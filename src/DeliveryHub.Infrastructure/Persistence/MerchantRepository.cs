using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Merchants;
using Microsoft.EntityFrameworkCore;

namespace DeliveryHub.Infrastructure.Persistence;

internal sealed class MerchantRepository : IMerchantRepository
{
    private readonly AppDbContext _db;

    public MerchantRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<bool> ExistePorIFoodIdAsync(Guid ifoodMerchantId, CancellationToken ct) =>
        _db.Merchants.AnyAsync(x => x.IFoodMerchantId == ifoodMerchantId, ct);

    public void Adicionar(Merchant merchant) => _db.Merchants.Add(merchant);
}
