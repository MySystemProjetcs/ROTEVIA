using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Couriers;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Application.Couriers;

public interface IDisponibilidadeEntrega
{
    Task<Result<bool>> ObterAsync(Guid usuarioId, CancellationToken ct);
    Task<Result<IReadOnlyList<Guid>>> DefinirAsync(Guid usuarioId, bool disponivel, CancellationToken ct);
}

// Interruptor manual Online/Offline do motoboy, global (vale pra todas as
// lojas). "Em Entrega" é derivado dos pedidos e não passa por aqui. Definir
// devolve as lojas ativas pra quem chama avisar cada painel.
public sealed class DisponibilidadeEntrega : IDisponibilidadeEntrega
{
    private readonly ICourierRepository _couriers;

    public DisponibilidadeEntrega(ICourierRepository couriers)
    {
        _couriers = couriers;
    }

    public async Task<Result<bool>> ObterAsync(Guid usuarioId, CancellationToken ct)
    {
        var courier = await _couriers.ObterPorUsuarioIdAsync(usuarioId, ct);
        if (courier is null)
            return Result.Failure<bool>(CourierErrors.VinculoNaoEncontrado);

        return Result.Success(courier.DisponivelParaEntrega);
    }

    public async Task<Result<IReadOnlyList<Guid>>> DefinirAsync(Guid usuarioId, bool disponivel, CancellationToken ct)
    {
        var courier = await _couriers.ObterPorUsuarioIdAsync(usuarioId, ct);
        if (courier is null)
            return Result.Failure<IReadOnlyList<Guid>>(CourierErrors.VinculoNaoEncontrado);

        courier.DefinirDisponibilidade(disponivel);
        await _couriers.SalvarAsync(ct);

        var lojas = await _couriers.ListarMerchantIdsAtivosAsync(courier.Id, ct);
        return Result.Success(lojas);
    }
}
