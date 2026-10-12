using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Merchants;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Application.Merchants;

public interface IVincularLojaNoventaENove
{
    Task<Result> ExecutarAsync(Guid merchantId, string appShopId, string? appId, CancellationToken ct);
}

// Vinculação é só `app_shop_id` — as credenciais globais do app (AppId/
// AppSecret) ficam em configuração, não por loja. O `auth_token` é obtido sob
// demanda pelo DiDiFoodAuthenticator a partir dessas duas peças.
public sealed class VincularLojaNoventaENove : IVincularLojaNoventaENove
{
    private readonly IMerchantRepository _merchants;

    public VincularLojaNoventaENove(IMerchantRepository merchants) => _merchants = merchants;

    public async Task<Result> ExecutarAsync(Guid merchantId, string appShopId, string? appId, CancellationToken ct)
    {
        var merchant = await _merchants.ObterPorIdAsync(merchantId, ct);
        if (merchant is null)
            return Result.Failure(EnderecoDaLojaErrors.LojaNaoEncontrada);

        var resultado = merchant.VincularNoventaENove(appShopId, appId);
        if (resultado.IsFailure)
            return resultado;

        await _merchants.SalvarAsync(ct);
        return Result.Success();
    }
}
