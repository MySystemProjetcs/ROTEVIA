using DeliveryHub.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace DeliveryHub.Infrastructure.Persistence;

internal sealed class EfIFoodMerchantSnapshotStore : IIFoodMerchantSnapshotStore
{
    private readonly AppDbContext _db;

    public EfIFoodMerchantSnapshotStore(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IFoodMerchantSnapshot?> ObterAsync(Guid merchantId, CancellationToken ct)
    {
        var snapshot = await _db.Set<IFoodMerchantSnapshotEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MerchantId == merchantId, ct);

        return snapshot is null
            ? null
            : new IFoodMerchantSnapshot(
                snapshot.MerchantId,
                snapshot.DetailsJson,
                snapshot.StatusJson,
                snapshot.OpeningHoursJson,
                snapshot.UpdatedAt);
    }

    public async Task SalvarAsync(IFoodMerchantSnapshot snapshot, CancellationToken ct)
    {
        var entidade = await _db.Set<IFoodMerchantSnapshotEntity>()
            .FirstOrDefaultAsync(x => x.MerchantId == snapshot.MerchantId, ct);

        if (entidade is null)
        {
            entidade = new IFoodMerchantSnapshotEntity { MerchantId = snapshot.MerchantId };
            _db.Add(entidade);
        }

        entidade.DetailsJson = snapshot.DetailsJson;
        entidade.StatusJson = snapshot.StatusJson;
        entidade.OpeningHoursJson = snapshot.OpeningHoursJson;
        entidade.UpdatedAt = snapshot.UpdatedAt;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IFoodMerchantSnapshot?> AtualizarHorarioAsync(
        Guid merchantId, string openingHoursJson, DateTimeOffset updatedAt, CancellationToken ct)
    {
        var entidade = await _db.Set<IFoodMerchantSnapshotEntity>()
            .FirstOrDefaultAsync(x => x.MerchantId == merchantId, ct);

        if (entidade is null)
            return null;

        entidade.OpeningHoursJson = openingHoursJson;
        entidade.UpdatedAt = updatedAt;
        await _db.SaveChangesAsync(ct);

        return new IFoodMerchantSnapshot(
            entidade.MerchantId,
            entidade.DetailsJson,
            entidade.StatusJson,
            entidade.OpeningHoursJson,
            entidade.UpdatedAt);
    }
}