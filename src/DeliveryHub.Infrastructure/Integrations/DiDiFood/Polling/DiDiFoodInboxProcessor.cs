using System.Text.Json;
using System.Diagnostics;
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
            var duracao = Stopwatch.StartNew();
            try
            {
                var resultado = await ProcessarAsync(entrada, ct);
                entrada.MarcarProcessado(_timeProvider.GetUtcNow());
                await _db.SaveChangesAsync(ct);
                processados++;

                _logger.LogInformation("Evento 99Food aplicado ao pedido e persistido. InboxId={InboxId} MerchantId={MerchantId} PedidoId={PedidoId} TipoEvento={TipoEvento} PedidoCriado={PedidoCriado} StatusAnterior={StatusAnterior} StatusNovo={StatusNovo} DuraçãoMs={DuracaoMs}",
                    entrada.Id, entrada.MerchantId, resultado.PedidoId, resultado.TipoEvento, resultado.PedidoCriado,
                    resultado.StatusAnterior, resultado.StatusNovo, duracao.Elapsed.TotalMilliseconds);

                await _notificador.ResumoAtualizadoAsync(entrada.MerchantId!.Value, ct);
            }
            catch (Exception ex)
            {
                _db.ChangeTracker.Clear();
                _logger.LogError(ex, "Falha ao processar evento 99Food do inbox. InboxId={InboxId} MerchantId={MerchantId} DuraçãoMs={DuracaoMs}",
                    entrada.Id, entrada.MerchantId, duracao.Elapsed.TotalMilliseconds);
            }
        }

        return processados;
    }

    private sealed record ResultadoProcessamento(Guid? PedidoId, string? TipoEvento, bool PedidoCriado, StatusPedido? StatusAnterior, StatusPedido? StatusNovo);

    private async Task<ResultadoProcessamento> ProcessarAsync(IntegrationInboxEvent entrada, CancellationToken ct)
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
                    "Evento 99Food sem objeto de pedido. InboxId={InboxId} MerchantId={MerchantId} TipoEvento={TipoEvento}",
                    entrada.Id, entrada.MerchantId, evento.EventType);
                return new ResultadoProcessamento(null, evento.EventType, false, null, null);
            }

            pedido = DiDiFoodOrderMapper.ParaPedido(evento.Order, entrada.MerchantId!.Value, entrada.ReceivedAt);
            _db.Pedidos.Add(pedido);
            return new ResultadoProcessamento(pedido.Id, evento.EventType, true, null, pedido.Status);
        }

        var statusAnterior = pedido.Status;
        AplicarTransicao(pedido, evento);
        return new ResultadoProcessamento(pedido.Id, evento.EventType, false, statusAnterior, pedido.Status);
    }

    // Nomes reais do guia da 99Food ("Receber informações sobre Pedidos"):
    // orderNew, orderCancel, orderFinish, deliveryStatus. orderNew só chega
    // aqui numa reentrega (a primeira vez cai no ramo "pedido is null" acima),
    // e deliveryStatus não distingue qual etapa mudou — as duas seguem pelo
    // status numérico do pedido, que é o único sinal granular disponível.
    private void AplicarTransicao(Pedido pedido, DiDiWebhookEvent evento)
    {
        Result resultado = evento.EventType switch
        {
            "orderCancel" => pedido.Cancelar(),
            "orderFinish" => pedido.Concluir(),
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
