using DeliveryHub.Infrastructure.Integrations.IFood.Polling;
using DeliveryHub.Worker.Health;

namespace DeliveryHub.Worker;

// Processador dedicado do Inbox (separação de responsabilidades — o plano
// pede polling ≠ processamento): o ciclo de polling reconhece eventos rápido e
// some; quem faz o trabalho pesado — buscar detalhe do pedido, confirmar,
// transicionar estado — é este loop, a cada 2s.
//
// Se um tick demorar ou falhar, o heartbeat do iFood não é afetado: ele mora
// no IFoodPollingWorker. E o contrário também vale — fila presa aqui gera
// alarme no InboxLagHealthCheck sem derrubar o polling.
public sealed class IFoodInboxProcessorWorker : BackgroundService
{
    // 2s: o plano recomenda 2–3s. O processador pega no máximo 100 por tick
    // (limite interno), então um lote grande só drena em ticks seguintes — e o
    // lag do Inbox fica visível enquanto isso.
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(2);
    private const int LimitePorTick = 100;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly InboxLagState _lag;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<IFoodInboxProcessorWorker> _logger;

    public IFoodInboxProcessorWorker(
        IServiceScopeFactory scopeFactory,
        InboxLagState lag,
        TimeProvider timeProvider,
        ILogger<IFoodInboxProcessorWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _lag = lag;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Intervalo);

        // Primeiro tick imediato: eventos que ficaram presos durante um restart
        // não podem esperar o primeiro interstício.
        do
        {
            await ExecutarTickAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ExecutarTickAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IIFoodInboxProcessor>();

            // Lag ANTES de processar: é o backlog real que o tick encontrou.
            // Publica também o horário do tick — parou de atualizar = processador
            // travado, que é justamente o que o InboxLagHealthCheck detecta.
            var (pendentes, maisAntigo) = await processor.LerLagAsync(ct);
            _lag.Registrar(pendentes, maisAntigo, _timeProvider.GetUtcNow());

            var processados = await processor.ProcessarPendentesAsync(limite: LimitePorTick, ct);

            if (processados > 0)
                _logger.LogInformation("Inbox: {Processados} evento(s) processado(s).", processados);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // O loop nunca pode morrer: exceção que escapa daqui encerra o
            // BackgroundService e a fila fica parada sem alarme. O lag do
            // InboxLagState também para de avançar e o health check acusa.
            _logger.LogError(ex, "Falha no tick do processador de Inbox.");
        }
    }
}

