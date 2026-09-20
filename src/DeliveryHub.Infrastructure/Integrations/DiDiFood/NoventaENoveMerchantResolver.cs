using DeliveryHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DeliveryHub.Infrastructure.Integrations.DiDiFood;

// Resolvedor de merchant exclusivo da 99Food. Não reaproveita IMerchantResolver
// (que é do iFood, busca por Guid via IFoodMerchantId) — o app_shop_id da
// 99Food é uma string livre, definida pelo integrador. Segregação total entre
// os dois marketplaces: cada um resolve o próprio tenant do próprio jeito.
public interface INoventaENoveMerchantResolver
{
    // Devolve nulo quando a loja não existe na nossa base — o evento vai para
    // quarentena em vez de virar pedido órfão.
    Task<Guid?> ResolverAsync(string appShopId, CancellationToken ct);
}

internal sealed class NoventaENoveMerchantResolver : INoventaENoveMerchantResolver
{
    private readonly AppDbContext _db;

    public NoventaENoveMerchantResolver(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Guid?> ResolverAsync(string appShopId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(appShopId))
            return null;

        return await _db.Merchants
            .AsNoTracking()
            .Where(x => x.NoventaENoveAppShopId == appShopId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);
    }
}
