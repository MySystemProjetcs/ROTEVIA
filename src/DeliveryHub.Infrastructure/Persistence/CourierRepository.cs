using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Couriers;
using Microsoft.EntityFrameworkCore;

namespace DeliveryHub.Infrastructure.Persistence;

internal sealed class CourierRepository : ICourierRepository
{
    private readonly AppDbContext _db;

    public CourierRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Courier?> ObterPorCpfAsync(string cpf, CancellationToken ct) =>
        _db.Couriers.FirstOrDefaultAsync(x => x.Cpf == cpf, ct);

    public Task<Courier?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        _db.Couriers.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<Courier?> ObterPorUsuarioIdAsync(Guid usuarioId, CancellationToken ct) =>
        _db.Couriers.FirstOrDefaultAsync(x => x.UsuarioId == usuarioId, ct);

    public void Adicionar(Courier courier) => _db.Couriers.Add(courier);

    // Sem filtro de tenant de propósito: quem chama é o confirmar-convite
    // público, sem sessão e sem merchant_id no contexto. O próprio linkId
    // (GUIDv7) mais o hash do token verificado na Application são a
    // autorização aqui — não a sessão de ninguém.
    public Task<CourierMerchantLink?> ObterLinkAsync(Guid linkId, CancellationToken ct) =>
        _db.CourierMerchantLinks.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == linkId, ct);

    public Task<bool> ExisteVinculoAtivoOuPendenteAsync(Guid courierId, Guid merchantId, CancellationToken ct) =>
        _db.CourierMerchantLinks.IgnoreQueryFilters().AnyAsync(
            x => x.CourierId == courierId
                && x.MerchantId == merchantId
                && x.Status != StatusVinculoEntregador.Suspenso,
            ct);

    public Task<bool> ExisteVinculoAtivoAsync(Guid courierId, Guid merchantId, CancellationToken ct) =>
        _db.CourierMerchantLinks.AnyAsync(
            x => x.CourierId == courierId && x.MerchantId == merchantId && x.Status == StatusVinculoEntregador.Ativo, ct);

    public void AdicionarLink(CourierMerchantLink link) => _db.CourierMerchantLinks.Add(link);

    public async Task<IReadOnlyList<EntregadorResumo>> ListarPorMerchantAsync(Guid merchantId, CancellationToken ct) =>
        await _db.CourierMerchantLinks
            .Where(link => link.MerchantId == merchantId)
            .Join(_db.Couriers, link => link.CourierId, courier => courier.Id, (link, courier) =>
                new EntregadorResumo(
                    link.Id, courier.Id, courier.Nome, courier.Telefone,
                    courier.ModeloDaMoto, courier.Placa, link.Status, courier.DisponivelParaEntrega))
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> ListarMerchantIdsAtivosAsync(Guid courierId, CancellationToken ct) =>
        await _db.CourierMerchantLinks
            // Sem filtro de tenant de propósito: o único chamador hoje é a
            // troca de disponibilidade, que roda com o token do motoboy (sem
            // merchant_id) — o filtro global devolveria lista vazia.
            .IgnoreQueryFilters()
            .Where(x => x.CourierId == courierId && x.Status == StatusVinculoEntregador.Ativo)
            .Select(x => x.MerchantId)
            .ToListAsync(ct);

    public Task SalvarAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
