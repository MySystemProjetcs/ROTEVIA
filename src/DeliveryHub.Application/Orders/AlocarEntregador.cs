using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Couriers;
using DeliveryHub.Domain.Orders;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Application.Orders;

public interface IAlocarEntregador
{
    Task<Result> ExecutarAsync(Guid pedidoId, Guid entregadorId, CancellationToken ct);
}

// Só define quem vai entregar — não avança o status nem toca no iFood. O
// clique em "Despachar" (AvancarPedido) é quem move o pedido de verdade.
public sealed class AlocarEntregador : IAlocarEntregador
{
    private readonly IPedidoRepository _pedidos;
    private readonly ICourierRepository _couriers;
    private readonly INotificadorPainel _notificador;

    public AlocarEntregador(IPedidoRepository pedidos, ICourierRepository couriers, INotificadorPainel notificador)
    {
        _pedidos = pedidos;
        _couriers = couriers;
        _notificador = notificador;
    }

    public async Task<Result> ExecutarAsync(Guid pedidoId, Guid entregadorId, CancellationToken ct)
    {
        var pedido = await _pedidos.ObterPorIdAsync(pedidoId, ct);
        if (pedido is null)
            return Result.Failure(AvancarPedidoErrors.PedidoNaoEncontrado);

        if (!await _couriers.ExisteVinculoAtivoAsync(entregadorId, pedido.MerchantId, ct))
            return Result.Failure(CourierErrors.VinculoNaoEncontrado);

        var resultado = pedido.AlocarEntregador(entregadorId);
        if (resultado.IsFailure)
            return resultado;

        await _pedidos.SalvarAsync(ct);
        await _notificador.ResumoAtualizadoAsync(pedido.MerchantId, ct);
        return Result.Success();
    }
}
