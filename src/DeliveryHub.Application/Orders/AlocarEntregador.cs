using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Couriers;
using DeliveryHub.Domain.Orders;
using DeliveryHub.Domain.SharedKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    private readonly ILogger<AlocarEntregador> _logger;

    public AlocarEntregador(
        IPedidoRepository pedidos,
        ICourierRepository couriers,
        INotificadorPainel notificador,
        ILogger<AlocarEntregador>? logger = null)
    {
        _pedidos = pedidos;
        _couriers = couriers;
        _notificador = notificador;
        _logger = logger ?? NullLogger<AlocarEntregador>.Instance;
    }

    public async Task<Result> ExecutarAsync(Guid pedidoId, Guid entregadorId, CancellationToken ct)
    {
        var pedido = await _pedidos.ObterPorIdAsync(pedidoId, ct);
        if (pedido is null)
        {
            _logger.LogWarning("Alocação de entregador recusada: pedido não encontrado. PedidoId={PedidoId}", pedidoId);
            return Result.Failure(AvancarPedidoErrors.PedidoNaoEncontrado);
        }

        if (!await _couriers.ExisteVinculoAtivoAsync(entregadorId, pedido.MerchantId, ct))
        {
            _logger.LogWarning("Alocação de entregador recusada: vínculo inativo ou ausente. PedidoId={PedidoId} MerchantId={MerchantId} EntregadorId={EntregadorId}",
                pedido.Id, pedido.MerchantId, entregadorId);
            return Result.Failure(CourierErrors.VinculoNaoEncontrado);
        }

        var resultado = pedido.AlocarEntregador(entregadorId);
        if (resultado.IsFailure)
        {
            _logger.LogWarning("Domínio recusou alocação de entregador. PedidoId={PedidoId} MerchantId={MerchantId} Erro={Erro}",
                pedido.Id, pedido.MerchantId, resultado.Error.Code);
            return resultado;
        }

        await _pedidos.SalvarAsync(ct);
        await _notificador.ResumoAtualizadoAsync(pedido.MerchantId, ct);
        _logger.LogInformation("Entregador alocado ao pedido. PedidoId={PedidoId} MerchantId={MerchantId} EntregadorId={EntregadorId} Status={Status}",
            pedido.Id, pedido.MerchantId, entregadorId, pedido.Status);
        return Result.Success();
    }
}
