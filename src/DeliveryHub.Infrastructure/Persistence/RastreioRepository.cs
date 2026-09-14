using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Tracking;

namespace DeliveryHub.Infrastructure.Persistence;

internal sealed class RastreioRepository : IRastreioRepository
{
    private readonly AppDbContext _db;

    public RastreioRepository(AppDbContext db)
    {
        _db = db;
    }

    public void Adicionar(PosicaoEntregador posicao) =>
        _db.PosicoesEntregador.Add(posicao);

    public Task SalvarAsync(CancellationToken ct) =>
        _db.SaveChangesAsync(ct);
}
