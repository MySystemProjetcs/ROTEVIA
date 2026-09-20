using System.Text.Json;
using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Orders;
using DeliveryHub.Domain.SharedKernel;
using DeliveryHub.Infrastructure.Integrations.DiDiFood.Contracts;
using DeliveryHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeliveryHub.Infrastructure.Integrations.DiDiFood.Polling;

public interface IDiDiFoodInboxProcessor
{
    Task<int> ProcessarPendentesAsync(int limite, CancellationToken ct);
}

// Processador do inbox exclusivo para eventos da 99Food / DiDi Food (SRP).
// Não mistura com o iFood: consome apenas entradas do inbox com Source == "99food".
internal sealed class DiDiFoodInboxProcessor : IDiDiFoodInboxProcessor
{
    public const string Origem = "99food";

    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly INotificadorPainel _notificador;
    private readonly ILogger<DiDiFoodInboxProcessor> _logger;

    public DiDiFoodInboxProcessor(
        AppDbContext db,
        TimeProvider timeProvider,
        INotificadorPainel notificador,
        ILogger<DiDiFoodInboxProcessor> logger)
    {
        _db = db;
        _timeProvider = timeProvider;
        _notificador = notificador;
        _logger = logger;
    }

    public async Task<int> ProcessarPendentesAsync(int limite, CancellationToken ct)
    {
        var pendentes = await _db.IntegrationInbox
            .Where(x => x.ProcessedAt == null && x.MerchantId != null && x.Source == Origem)
            .OrderBy(x => x.ReceivedAt)
            .Take(limite)
            .ToListAsync(ct);

        var processados = 0;

        foreach (var entrada in pendentes)
        {
            try
            {
                await ProcessarAsync(entrada, ct);
                entrada.MarcarProcessado(_timeProvider.GetUtcNow());
                await _db.SaveChangesAsync(ct);
                processados++;

                await _notificador.ResumoAtualizadoAsync(entrada.MerchantId!.Value, ct);
            }
            catch (Exception ex)
            {
                _db.ChangeTracker.Clear();
                _logger.LogError(ex, "Falha ao processar evento 99Food {EventoId} do inbox.", entrada.ExternalEventId);
            }
        }

        return processados;
    }

    private async Task ProcessarAsync(IntegrationInboxEvent entrada, CancellationToken ct)
    {
        var evento = JsonSerializer.Deserialize<DiDiWebhookEvent>(entrada.Payload)
            ?? throw new InvalidOperationException("Payload do inbox da 99Food ilegível.");

        var idExterno = evento.OrderId.ToString();

        var pedido = await _db.Pedidos
            .FirstOrDefaultAsync(x => x.IdExterno == idExterno, ct);

        if (pedido is null)
        {
            if (evento.Order is null)
            {
                _logger.LogWarning(
                    "Evento {EventType} da 99Food para o pedido {PedidoId} sem objeto Order.",
                    evento.EventType, idExterno);
                return;
            }

            pedido = DiDiFoodOrderMapper.ParaPedido(evento.Order, entrada.MerchantId!.Value, entrada.ReceivedAt);
            _db.Pedidos.Add(pedido);
            return;
        }

        AplicarTransicao(pedido, evento);
    }

    private void AplicarTransicao(Pedido pedido, DiDiWebhookEvent evento)
    {
        Result resultado = evento.EventType switch
        {
            "confirmOrder" => pedido.Confirmar(),
            "startPreparation" => pedido.IniciarPreparo(),
            "readyToPickup" => pedido.MarcarPronto(),
            "dispatchOrder" => pedido.Despachar(),
            "completeOrder" => pedido.Concluir(),
            "cancelOrder" => pedido.Cancelar(),
            _ => ProcessarPorStatus(pedido, evento.Order?.Status)
        };

        if (resultado.IsFailure)
        {
            _logger.LogWarning(
                "Transição {EventType} da 99Food recusada para o pedido {PedidoId}: {Erro}",
                evento.EventType, pedido.IdExterno, resultado.Error.Code);
        }
    }

    private static Result ProcessarPorStatus(Pedido pedido, int? status) => status switch
    {
        2 => pedido.Confirmar(),
        3 => pedido.MarcarPronto(),
        4 => pedido.Despachar(),
        5 => pedido.Concluir(),
        >= 6 => pedido.Cancelar(),
        _ => Result.Success()
    };
}
