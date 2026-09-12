using Microsoft.EntityFrameworkCore;

namespace DeliveryHub.Infrastructure.Persistence;

public interface IMerchantResolver
{
    // Devolve nulo quando a loja não existe na nossa base — o evento vai para
    // quarentena em vez de virar pedido órfão.
    Task<Guid?> ResolverAsync(Guid ifoodMerchantId, CancellationToken ct);
}

internal sealed class MerchantResolver : IMerchantResolver
{
    private readonly AppDbContext _db;

    public MerchantResolver(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Guid?> ResolverAsync(Guid ifoodMerchantId, CancellationToken ct)
    {
        // Roda a cada evento: leitura projetada, sem tracking, apoiada no índice
        // único ux_merchants_ifood_id.
        var id = await _db.Merchants
            .AsNoTracking()
            .Where(x => x.IFoodMerchantId == ifoodMerchantId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);

        return id;
    }
}
