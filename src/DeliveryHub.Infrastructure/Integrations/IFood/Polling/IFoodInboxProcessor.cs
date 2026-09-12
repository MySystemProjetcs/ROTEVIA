using System.Text.Json;
using DeliveryHub.Domain.Orders;
using DeliveryHub.Domain.SharedKernel;
using DeliveryHub.Infrastructure.Integrations.IFood.Orders;
using DeliveryHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Polling;

public interface IIFoodInboxProcessor
{
    Task<int> ProcessarPendentesAsync(int limite, CancellationToken ct);
}

// Roda separado do polling (CLAUDE.md §7): a ingestão grava e reconhece rápido,
// o trabalho pesado — buscar detalhe do pedido — acontece aqui, fora da janela
// de 30s do heartbeat.
internal sealed class IFoodInboxProcessor : IIFoodInboxProcessor
{
    private readonly AppDbContext _db;
    private readonly IIFoodOrderClient _orders;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<IFoodInboxProcessor> _logger;

    public IFoodInboxProcessor(
        AppDbContext db,
        IIFoodOrderClient orders,
        TimeProvider timeProvider,
        ILogger<IFoodInboxProcessor> logger)
    {
        _db = db;
        _orders = orders;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<int> ProcessarPendentesAsync(int limite, CancellationToken ct)
    {
        // Eventos em quarentena (merchant_id nulo) ficam de fora: são
        // reprocessáveis depois que a loja for cadastrada.
        var pendentes = await _db.IntegrationInbox
            .Where(x => x.ProcessedAt == null && x.MerchantId != null)
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
            }
            catch (Exception ex)
            {
                // Uma entrada ruim não pode travar a fila inteira: ela fica
                // pendente e é retentada no próximo ciclo.
                _db.ChangeTracker.Clear();
                _logger.LogError(ex, "Falha ao processar evento {EventoId} do inbox.", entrada.ExternalEventId);
            }
        }

        return processados;
    }

    private async Task ProcessarAsync(IntegrationInboxEvent entrada, CancellationToken ct)
    {
        var evento = JsonSerializer.Deserialize<Contracts.IFoodEvent>(entrada.Payload)
            ?? throw new InvalidOperationException("Payload do inbox ilegível.");

        var idExterno = evento.OrderId.ToString();

        var pedido = await _db.Pedidos
            .FirstOrDefaultAsync(x => x.IdExterno == idExterno, ct);

        if (pedido is null)
        {
            // Só o PLACED cria pedido. Qualquer outro evento sem pedido
            // correspondente chegou fora de ordem — o PLACED ainda está na fila.
            if (evento.FullCode != "PLACED")
            {
                _logger.LogWarning(
                    "Evento {FullCode} do pedido {PedidoId} chegou antes do PLACED; será retentado.",
                    evento.FullCode, idExterno);
                throw new InvalidOperationException("Pedido ainda não existe.");
            }

            var detalhe = await _orders.GetDetailsAsync(evento.OrderId, ct);
            pedido = IFoodOrderMapper.ParaPedido(detalhe, entrada.MerchantId!.Value, entrada.ReceivedAt);
            _db.Pedidos.Add(pedido);
            return;
        }

        AplicarTransicao(pedido, evento.FullCode);
    }

    private void AplicarTransicao(Pedido pedido, string fullCode)
    {
        var resultado = fullCode switch
        {
            "PLACED" => Result.Success(),
            "CONFIRMED" => pedido.Confirmar(),
            "PREPARATION_STARTED" => pedido.IniciarPreparo(),
            "READY_TO_PICKUP" => pedido.MarcarPronto(),
            "DISPATCHED" => pedido.Despachar(),
            "CONCLUDED" => pedido.Concluir(),
            "CANCELLED" => pedido.Cancelar(),
            // Eventos que não mudam status (logística, handshake, patch) são
            // reconhecidos e marcados como processados sem efeito no pedido.
            _ => Result.Success()
        };

        if (resultado.IsFailure)
        {
            _logger.LogWarning(
                "Transição {FullCode} recusada para o pedido {PedidoId}: {Erro}",
                fullCode, pedido.IdExterno, resultado.Error.Code);
        }
    }
}
