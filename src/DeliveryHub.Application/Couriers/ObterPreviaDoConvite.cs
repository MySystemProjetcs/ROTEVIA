using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Couriers;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Application.Couriers;

// Só o suficiente pra tela pública mostrar "Olá {nome}, convite de {loja}"
// antes do motoboy digitar o token — nunca o hash, nunca dado de outro tenant.
public sealed record PreviaDoConvite(string NomeEntregador, string NomeLoja, bool Valido);

public interface IObterPreviaDoConvite
{
    Task<Result<PreviaDoConvite>> ExecutarAsync(Guid linkId, CancellationToken ct);
}

public sealed class ObterPreviaDoConvite : IObterPreviaDoConvite
{
    private readonly ICourierRepository _couriers;
    private readonly IMerchantRepository _merchants;
    private readonly TimeProvider _timeProvider;

    public ObterPreviaDoConvite(ICourierRepository couriers, IMerchantRepository merchants, TimeProvider timeProvider)
    {
        _couriers = couriers;
        _merchants = merchants;
        _timeProvider = timeProvider;
    }

    public async Task<Result<PreviaDoConvite>> ExecutarAsync(Guid linkId, CancellationToken ct)
    {
        var link = await _couriers.ObterLinkAsync(linkId, ct);
        if (link is null)
            return Result.Failure<PreviaDoConvite>(CourierErrors.VinculoNaoEncontrado);

        var courier = await _couriers.ObterPorIdAsync(link.CourierId, ct)
            ?? throw new InvalidOperationException("CourierMerchantLink aponta para Courier inexistente.");
        var merchant = await _merchants.ObterPorIdAsync(link.MerchantId, ct)
            ?? throw new InvalidOperationException("CourierMerchantLink aponta para Merchant inexistente.");

        var valido = link.Status == StatusVinculoEntregador.Convidado
            && link.ConviteExpiraEm is not null
            && link.ConviteExpiraEm > _timeProvider.GetUtcNow();

        return Result.Success(new PreviaDoConvite(courier.Nome, merchant.Nome, valido));
    }
}
