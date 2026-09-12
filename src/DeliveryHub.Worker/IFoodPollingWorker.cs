using DeliveryHub.Infrastructure.Integrations.IFood;
using DeliveryHub.Infrastructure.Integrations.IFood.Polling;

namespace DeliveryHub.Worker;

public sealed class IFoodPollingWorker : BackgroundService
{
    // Intervalo do heartbeat. Sem requisição regular a cada 30s o iFood marca
    // a loja como offline e para de mandar pedido (CLAUDE.md §7).
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(30);

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

            var resultado = await ingestor.IngerirAsync(ct);
            _health.RegistrarSucesso();

            // Processamento fica depois do ack: se a busca de detalhe demorar,
            // o heartbeat do próximo ciclo não é afetado.
            var processados = await scope.ServiceProvider
                .GetRequiredService<IIFoodInboxProcessor>()
                .ProcessarPendentesAsync(limite: 50, ct);

            if (processados > 0)
                _logger.LogInformation("Inbox: {Processados} evento(s) processado(s).", processados);

            if (resultado.Recebidos > 0)
            {
                _logger.LogInformation(
                    "Polling: {Recebidos} evento(s), {Gravados} gravado(s), {Duplicados} duplicado(s), {Quarentena} em quarentena.",
                    resultado.Recebidos, resultado.Gravados, resultado.Duplicados, resultado.EmQuarentena);
            }
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
}
