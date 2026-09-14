using DeliveryHub.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace DeliveryHub.Infrastructure.RealTime;

// INotificadorPainel falando SignalR. Falha de transporte não quebra o caso
// de uso que chamou: o painel também recarrega por polling, o push é
// antecipação, não a única via.
internal sealed class NotificadorPainel : INotificadorPainel
{
    private readonly IHubContext<RastreioHub> _hub;
    private readonly ILogger<NotificadorPainel> _logger;

    public NotificadorPainel(IHubContext<RastreioHub> hub, ILogger<NotificadorPainel> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task ResumoAtualizadoAsync(Guid merchantId, CancellationToken ct)
    {
        try
        {
            await _hub.Clients
                .Group(RastreioHub.GrupoDoMerchant(merchantId))
                .SendAsync(RastreioHub.MetodoResumoAtualizado, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao notificar resumo do painel da loja {MerchantId}.", merchantId);
        }
    }
}
