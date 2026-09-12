using DeliveryHub.Domain.Orders;

namespace DeliveryHub.Application.Abstractions;

public interface IPedidoRepository
{
    Task<Pedido?> ObterPorIdAsync(Guid id, CancellationToken ct);
    Task SalvarAsync(CancellationToken ct);
}
