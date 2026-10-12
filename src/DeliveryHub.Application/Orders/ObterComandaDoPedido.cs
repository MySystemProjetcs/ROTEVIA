using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Orders;
using DeliveryHub.Domain.SharedKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeliveryHub.Application.Orders;

public interface IObterComandaDoPedido
{
    Task<Result<PedidoReceiptSnapshot>> ExecutarAsync(Guid pedidoId, CancellationToken ct);
}

public sealed class ObterComandaDoPedido : IObterComandaDoPedido
{
    private readonly IPedidoRepository _pedidos;
    private readonly ILogger<ObterComandaDoPedido> _logger;

    public ObterComandaDoPedido(IPedidoRepository pedidos, ILogger<ObterComandaDoPedido>? logger = null)
    {
        _pedidos = pedidos;
        _logger = logger ?? NullLogger<ObterComandaDoPedido>.Instance;
    }

    public async Task<Result<PedidoReceiptSnapshot>> ExecutarAsync(Guid pedidoId, CancellationToken ct)
    {
        var pedido = await _pedidos.ObterPorIdAsync(pedidoId, ct);
        if (pedido is null)
        {
            _logger.LogWarning("Comanda não encontrada. PedidoId={PedidoId}", pedidoId);
            return Result.Failure<PedidoReceiptSnapshot>(AvancarPedidoErrors.PedidoNaoEncontrado);
        }

        // Pedidos recebidos antes da coluna receipt_data vêm com snapshot nulo.
        // Reimpressão deles cai neste fallback — o builder monta a comanda a
        // partir dos campos atuais do próprio pedido. Preços e itens estão lá
        // porque o Pedido mesmo é imutável nesses campos (não há edição
        // retroativa que invalidaria o resultado).
        var comanda = pedido.ReceiptData ?? ComandaBuilder.Montar(pedido);
        _logger.LogInformation("Comanda obtida. PedidoId={PedidoId} MerchantId={MerchantId} ItemCount={ItemCount} SnapshotPersistido={SnapshotPersistido}",
            pedido.Id, pedido.MerchantId, comanda.Itens.Count, pedido.ReceiptData is not null);
        return Result.Success(comanda);
    }
}
