using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Orders;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Application.Orders;

public interface IObterComandaDoPedido
{
    Task<Result<PedidoReceiptSnapshot>> ExecutarAsync(Guid pedidoId, CancellationToken ct);
}

public sealed class ObterComandaDoPedido : IObterComandaDoPedido
{
    private readonly IPedidoRepository _pedidos;

    public ObterComandaDoPedido(IPedidoRepository pedidos) => _pedidos = pedidos;

    public async Task<Result<PedidoReceiptSnapshot>> ExecutarAsync(Guid pedidoId, CancellationToken ct)
    {
        var pedido = await _pedidos.ObterPorIdAsync(pedidoId, ct);
        if (pedido is null)
            return Result.Failure<PedidoReceiptSnapshot>(AvancarPedidoErrors.PedidoNaoEncontrado);

        // Pedidos recebidos antes da coluna receipt_data vêm com snapshot nulo.
        // Reimpressão deles cai neste fallback — o builder monta a comanda a
        // partir dos campos atuais do próprio pedido. Preços e itens estão lá
        // porque o Pedido mesmo é imutável nesses campos (não há edição
        // retroativa que invalidaria o resultado).
        return Result.Success(pedido.ReceiptData ?? ComandaBuilder.Montar(pedido));
    }
}
