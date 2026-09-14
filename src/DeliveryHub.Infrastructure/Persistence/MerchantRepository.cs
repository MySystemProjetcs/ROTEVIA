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

    public Task<bool> ExistePorIFoodIdAsync(Guid ifoodMerchantId, Guid excetoMerchantId, CancellationToken ct) =>
        _db.Merchants.AnyAsync(x => x.IFoodMerchantId == ifoodMerchantId && x.Id != excetoMerchantId, ct);

    // Com tracking: quem chama vai confirmar/atualizar a conexão iFood.
    public Task<Merchant?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        _db.Merchants.FirstOrDefaultAsync(x => x.Id == id, ct);

    // Tracked (sem AsNoTracking): o worker pode renovar o token e salvar de
    // volta na mesma instância dentro do ciclo.
    public async Task<IReadOnlyList<Merchant>> ListarConectadosAsync(CancellationToken ct) =>
        await _db.Merchants
            .Where(x => x.ConexaoIFood != null && x.ConexaoIFood.AccessToken != null)
            .ToListAsync(ct);

    public void Adicionar(Merchant merchant) => _db.Merchants.Add(merchant);
    public Task SalvarAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
