using DeliveryHub.Application.Abstractions;
using DeliveryHub.Infrastructure.Integrations.IFood;
using DeliveryHub.Infrastructure.Integrations.IFood.Polling;

namespace DeliveryHub.Worker;

public sealed class IFoodPollingWorker : BackgroundService
{
    // 30s é o teto (sem requisição regular a cada 30s o iFood marca a loja
    // como offline — CLAUDE.md §7), não o mínimo. Consultar mais rápido é
    // permitido e reduz o atraso entre o pedido existir no iFood e aparecer
    // no painel — 8s ainda fica bem longe de qualquer limite de taxa deles.
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(8);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPollingHealth _health;
    private readonly ILogger<IFoodPollingWorker> _logger;

    public IFoodPollingWorker(
        IServiceScopeFactory scopeFactory,
        IPollingHealth health,
        ILogger<IFoodPollingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _health = health;
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
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var ingestor = scope.ServiceProvider.GetRequiredService<IIFoodEventIngestor>();

            // Cada loja do fluxo Distribuído tem seu próprio token e seu
            // próprio ciclo de polling — o Centralizado nunca as enxergaria.
            // Roda antes do Centralizado: numa loja com os dois apps ativos
            // (cenário só possível em homologação, uma loja real nunca tem os
            // dois ao mesmo tempo) o mesmo pedido é enfileirado nas duas
            // filas — quem faz o polling primeiro grava, o outro bate na
            // idempotência e vira "duplicado". Sem essa ordem, o Distribuído
            // nunca ganha a corrida contra o Centralizado.
            var merchants = scope.ServiceProvider.GetRequiredService<IMerchantRepository>();
            var tokenProvider = scope.ServiceProvider.GetRequiredService<IIFoodMerchantTokenProvider>();
            var conectados = await merchants.ListarConectadosAsync(ct);

            foreach (var merchant in conectados)
            {
                try
                {
                    var autorizacao = await tokenProvider.ObterAsync(merchant, ct);
                    var resultado = await ingestor.IngerirAsync(ct, autorizacao);
                    LogResultado($"loja {merchant.Nome}", resultado);
                }
                catch (Exception exLoja)
                {
                    // Uma loja falhando (token revogado, rede) não pode
                    // derrubar as outras nem o Centralizado — cada uma tem seu
                    // próprio raio de falha.
                    _logger.LogError(exLoja, "Falha no polling da loja {MerchantId} ({Nome}).", merchant.Id, merchant.Nome);
                }
            }

            // Sem token explícito: pega o Centralizado, que ainda cobre as
            // lojas cadastradas do jeito antigo (CLAUDE.md §7 — comportamento
            // inalterado para quem já estava em produção).
            var resultadoCentralizado = await ingestor.IngerirAsync(ct);
            LogResultado("Centralizado", resultadoCentralizado);

            _health.RegistrarSucesso();

            // Processamento fica depois do ack: se a busca de detalhe demorar,
            // o heartbeat do próximo ciclo não é afetado.
            var processados = await scope.ServiceProvider
                .GetRequiredService<IIFoodInboxProcessor>()
                .ProcessarPendentesAsync(limite: 50, ct);

            if (processados > 0)
                _logger.LogInformation("Inbox: {Processados} evento(s) processado(s).", processados);
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
