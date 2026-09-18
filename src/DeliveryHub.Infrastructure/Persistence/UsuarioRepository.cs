using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace DeliveryHub.Infrastructure.Persistence;

internal sealed class UsuarioRepository : IUsuarioRepository
{
    private readonly AppDbContext _db;

    public UsuarioRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken ct) =>
        _db.Usuarios.FirstOrDefaultAsync(x => x.Email == email.Trim().ToLower(), ct);

    public Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        _db.Usuarios.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> ExisteComEmailAsync(string email, CancellationToken ct) =>
        _db.Usuarios.AnyAsync(x => x.Email == email.Trim().ToLower(), ct);

    public async Task<Guid?> ObterMerchantVinculadoAsync(Guid usuarioId, CancellationToken ct) =>
        await _db.UsuarioMerchants
            .AsNoTracking()
            .Where(x => x.UsuarioId == usuarioId)
            .Select(x => (Guid?)x.MerchantId)
            .FirstOrDefaultAsync(ct);

    public void Adicionar(Usuario usuario) => _db.Usuarios.Add(usuario);
    public void Vincular(UsuarioMerchant vinculo) => _db.UsuarioMerchants.Add(vinculo);
    public Task SalvarAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
