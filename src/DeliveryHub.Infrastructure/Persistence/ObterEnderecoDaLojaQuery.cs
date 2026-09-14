using DeliveryHub.Application.Merchants;
using Microsoft.EntityFrameworkCore;

namespace DeliveryHub.Infrastructure.Persistence;

internal sealed class ObterEnderecoDaLojaQuery : IObterEnderecoDaLoja
{
    private readonly AppDbContext _db;

    public ObterEnderecoDaLojaQuery(AppDbContext db)
    {
        _db = db;
    }

    public Task<EnderecoDaLojaDto?> ExecutarAsync(Guid merchantId, CancellationToken ct) =>
        _db.Merchants
            .AsNoTracking()
            .Where(m => m.Id == merchantId && m.Endereco != null)
            .Select(m => new EnderecoDaLojaDto(
                m.Endereco!.Logradouro + ", " + m.Endereco.Numero + " - " + m.Endereco.Bairro,
                m.Endereco.Latitude,
                m.Endereco.Longitude))
            .FirstOrDefaultAsync(ct);
}
