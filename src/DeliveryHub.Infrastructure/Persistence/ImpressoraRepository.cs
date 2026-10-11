using DeliveryHub.Application.Merchants;
using DeliveryHub.Domain.Merchants;
using Microsoft.EntityFrameworkCore;

namespace DeliveryHub.Infrastructure.Persistence;

internal sealed class ImpressoraRepository : IImpressoraRepository
{
    private readonly AppDbContext _db;

    public ImpressoraRepository(AppDbContext db) => _db = db;

    public Task<ImpressoraDaLoja?> ObterAsync(Guid merchantId, Guid impressoraId, CancellationToken ct) =>
        _db.ImpressorasDaLoja.FirstOrDefaultAsync(x => x.Id == impressoraId && x.MerchantId == merchantId, ct);

    public async Task<IReadOnlyList<ImpressoraDaLoja>> ListarAsync(Guid merchantId, CancellationToken ct) =>
        await _db.ImpressorasDaLoja
            .AsNoTracking()
            .Where(x => x.MerchantId == merchantId && x.Ativa)
            .OrderBy(x => x.CriadaEm)
            .ToListAsync(ct);

    public void Adicionar(ImpressoraDaLoja impressora) => _db.ImpressorasDaLoja.Add(impressora);
    public void Remover(ImpressoraDaLoja impressora) => impressora.Desativar();
    public Task SalvarAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
