using DeliveryHub.Application.Abstractions;

namespace DeliveryHub.Application.Couriers;

public interface IListarEntregadores
{
    Task<IReadOnlyList<EntregadorResumo>> ExecutarAsync(Guid merchantId, CancellationToken ct);
}

public sealed class ListarEntregadores : IListarEntregadores
{
    private readonly ICourierRepository _couriers;

    public ListarEntregadores(ICourierRepository couriers)
    {
        _couriers = couriers;
    }

    public Task<IReadOnlyList<EntregadorResumo>> ExecutarAsync(Guid merchantId, CancellationToken ct) =>
        _couriers.ListarPorMerchantAsync(merchantId, ct);
}
