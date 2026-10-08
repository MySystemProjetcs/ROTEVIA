using System.Diagnostics;
using DeliveryHub.Application.Abstractions;
using DeliveryHub.Infrastructure.Integrations.IFood;
using DeliveryHub.Infrastructure.Integrations.IFood.Polling;
using DeliveryHub.Worker.Resilience;
using Polly.CircuitBreaker;

namespace DeliveryHub.Worker;

// Ciclo de polling SÓ — grava no Inbox e reconhece. O trabalho pesado (buscar
// detalhe do pedido, confirmar, transicionar) mora no IFoodInboxProcessorWorker:
// separar os dois é o que garante que uma busca demorada não atrapalhe o
// heartbeat (o plano §1, e CLAUDE.md §7: "desacople ingestão de
// processamento").
public sealed class IFoodPollingWorker : BackgroundService
{
    // 30s é o teto (sem requisição regular a cada 30s o iFood marca a loja
    // como offline — CLAUDE.md §7), não o mínimo. Consultar mais rápido é
    // permitido e reduz o atraso entre o pedido existir no iFood e aparecer
    // no painel — 8s ainda fica bem longe de qualquer limite de taxa deles.
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(8);

    // Lojas em paralelo por ciclo (plano §2: comece com 4–8). Cada task cria o
    // próprio scope — o semáforo é o que impede 50 lojas de abrirem 50 scopes
    // e 50 conexões de uma vez.
    private const int ConcorrenciaMaxima = 6;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPollingHealth _health;
    private readonly MerchantResilienceProvider _resiliencia;
    private readonly ILogger<IFoodPollingWorker> _logger;
    private readonly SemaphoreSlim _limite = new(ConcorrenciaMaxima, ConcorrenciaMaxima);

    public IFoodPollingWorker(
        IServiceScopeFactory scopeFactory,
        IPollingHealth health,
        MerchantResilienceProvider resiliencia,
        ILogger<IFoodPollingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _health = health;
        _resiliencia = resiliencia;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Intervalo);

        // Primeiro ciclo imediato: esperar 30s no start atrasaria o heartbeat
        // logo depois de um restart, que é justamente quando ele mais importa.
        do
        {
            await ExecutarCicloAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ExecutarCicloAsync(CancellationToken ct)
    {
        var relogio = Stopwatch.StartNew();

        try
        {
            var conectados = await ListarLojasConectadasAsync(ct);

            // Distribuídas em paralelo. O WhenAll preserva a ordem em relação
            // ao Centralizado abaixo: numa loja com os dois apps ativos o mesmo
            // pedido é enfileirado nas duas filas — quem faz o polling primeiro
            // grava, o outro bate na idempotência. Paralelo dentro do
            // Distribuído não muda essa corrida; só não pode passar na frente
            // do Centralizado (comentário original do ciclo sequencial).
            var tarefas = conectados.Select(loja => ProcessarLojaAsync(loja.Id, loja.Nome, ct));
            await Task.WhenAll(tarefas);

            // Sem token explícito: pega o Centralizado, que ainda cobre as
            // lojas cadastradas do jeito antigo (CLAUDE.md §7 — comportamento
            // inalterado para quem já estava em produção).
            using (var scope = _scopeFactory.CreateScope())
            {
                var ingestor = scope.ServiceProvider.GetRequiredService<IIFoodEventIngestor>();
                var resultadoCentralizado = await ingestor.IngerirAsync(ct);
                LogResultado("Centralizado", resultadoCentralizado);
            }

            _health.RegistrarSucesso();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (IFoodApiException ex)
        {
            _health.RegistrarFalha(ex.Message);

            if (ex.UnauthorizedMerchants.Count > 0)
            {
                // O iFood diz exatamente quais lojas perderam autorização, em
                // vez de falhar em bloco. Repetir não resolve: a doc é explícita
                // que 403 não deve ter retentativa automática.
                _logger.LogError(
                    "Polling recusado para {Quantidade} loja(s) sem autorização: {Lojas}",
                    ex.UnauthorizedMerchants.Count, string.Join(", ", ex.UnauthorizedMerchants));
            }
            else
            {
                _logger.LogError(ex, "Falha no ciclo de polling do iFood.");
            }
        }
        catch (Exception ex)
        {
            // O ciclo nunca pode morrer: se a exceção escapar daqui, o
            // BackgroundService encerra e a loja fica offline no iFood.
            _health.RegistrarFalha(ex.Message);
            _logger.LogError(ex, "Falha inesperada no ciclo de polling.");
        }
        finally
        {
            MetricasPolling.RegistrarDuracaoDoCiclo(relogio.Elapsed);
        }
    }


    private async Task<List<(Guid Id, string Nome)>> ListarLojasConectadasAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var merchants = scope.ServiceProvider.GetRequiredService<IMerchantRepository>();

        // Projeção para valores antes de sair do scope: a lista inteira com
        // entidades rastreadas viveria além do DbContext que a carregou, e as
        // tasks paralelas precisam de instâncias do PRÓPRIO scope (token
        // renovado só persiste se o merchant estiver rastreado no contexto que
        // chama SaveChanges).
        var conectados = await merchants.ListarConectadosAsync(ct);
        return conectados.Select(m => (m.Id, m.Nome)).ToList();
    }

    private async Task ProcessarLojaAsync(Guid merchantId, string nome, CancellationToken ct)
    {
        await _limite.WaitAsync(ct);

        try
        {
            // Scope por task: DbContext não é thread-safe e o tokenProvider
            // pode salvar o merchant (renovação) — salvar uma entidade
            // rastreada por OUTRO scope é falha silenciosa (nada é gravado).
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;

            var merchant = await sp.GetRequiredService<IMerchantRepository>()
                .ObterPorIdAsync(merchantId, ct);

            if (merchant?.ConexaoIFood is null)
            {
                // Desconectou entre a listagem e o polling deste ciclo — não é
                // erro, a próxima listagem já não a retorna.
                return;
            }

            var tokenProvider = sp.GetRequiredService<IIFoodMerchantTokenProvider>();
            var ingestor = sp.GetRequiredService<IIFoodEventIngestor>();

            // Timeout + circuit breaker por loja (plano §3–4): uma loja
            // travada não segura o slot do semáforo nem derruba as demais.
            var resultado = await _resiliencia.ExecutarAsync(merchant.Id, ct, async token =>
            {
                var autorizacao = await tokenProvider.ObterAsync(merchant, token);
                return await ingestor.IngerirAsync(token, autorizacao);
            });

            LogResultado($"loja {nome}", resultado);
            MetricasPolling.RegistrarSucesso(merchant.Id);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (BrokenCircuitException ex)
        {
            // Circuito já aberto para esta loja: o plano manda isolar e seguir.
            // Conta como falha da loja (ela não foi consultada), não do ciclo.
            MetricasPolling.RegistrarFalha(merchantId);
            _logger.LogWarning(
                ex,
                "Polling da loja {MerchantId} ({Nome}) em cooldown — circuit breaker aberto.",
                merchantId, nome);
        }
        catch (Exception ex)
        {
            // Uma loja falhando (token revogado, rede) não pode derrubar as
            // outras nem o Centralizado — cada uma tem seu próprio raio de
            // falha. Timeout/rede são exceções de infra, não de negócio.
            MetricasPolling.RegistrarFalha(merchantId);
            _logger.LogError(ex, "Falha no polling da loja {MerchantId} ({Nome}).", merchantId, nome);
        }
        finally
        {
            _limite.Release();
        }
    }

    private void LogResultado(string origem, ResultadoIngestao resultado)
    {
        if (resultado.Recebidos == 0)
            return;

        _logger.LogInformation(
            "Polling [{Origem}]: {Recebidos} evento(s), {Gravados} gravado(s), {Duplicados} duplicado(s), {Quarentena} em quarentena.",
            origem, resultado.Recebidos, resultado.Gravados, resultado.Duplicados, resultado.EmQuarentena);
    }
}
