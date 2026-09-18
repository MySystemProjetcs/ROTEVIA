using DeliveryHub.Application.Identity;
using DeliveryHub.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace DeliveryHub.Infrastructure.Persistence;

// Perfil de quem está logado. Sempre por UsuarioId vindo do token: não existe
// versão deste endpoint que leia o id de outra pessoa.
internal sealed class PerfilQuery : IObterPerfil
{
    private readonly AppDbContext _db;

    public PerfilQuery(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Perfil?> ExecutarAsync(Guid usuarioId, CancellationToken ct)
    {
        var usuario = await _db.Usuarios
            .AsNoTracking()
            .Where(x => x.Id == usuarioId)
            .Select(x => new
            {
                x.Nome,
                x.Email,
                x.Papel,
                x.FotoBase64,
                x.CriadoEm,
            })
            .FirstOrDefaultAsync(ct);

        if (usuario is null)
            return null;

        return new Perfil(
            usuario.Nome,
            usuario.Email,
            usuario.Papel,
            usuario.FotoBase64,
            usuario.CriadoEm,
            usuario.Papel == PapelUsuario.DonoRestaurante ? await LojaDoDonoAsync(usuarioId, ct) : null,
            usuario.Papel == PapelUsuario.Entregador ? await DadosDoEntregadorAsync(usuarioId, ct) : null);
    }

    // IgnoreQueryFilters: o filtro global de tenant depende do MerchantId do
    // contexto, e é justamente a loja do usuário que estamos descobrindo aqui.
    // O recorte de segurança é o join por usuario_merchants, não o filtro.
    private Task<PerfilDaLoja?> LojaDoDonoAsync(Guid usuarioId, CancellationToken ct) =>
        _db.Merchants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => _db.UsuarioMerchants.Any(v => v.UsuarioId == usuarioId && v.MerchantId == m.Id))
            .Select(m => new PerfilDaLoja(
                m.Nome,
                m.Endereco == null
                    ? null
                    : m.Endereco.Logradouro + ", " + m.Endereco.Numero + " — " + m.Endereco.Bairro
                        + ", " + m.Endereco.Cidade + "/" + m.Endereco.Estado,
                m.TaxaPadraoPorEntrega))
            .FirstOrDefaultAsync(ct);

    private Task<PerfilDoEntregador?> DadosDoEntregadorAsync(Guid usuarioId, CancellationToken ct) =>
        _db.Couriers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.UsuarioId == usuarioId)
            .Select(c => new PerfilDoEntregador(
                c.Cpf,
                c.Telefone,
                c.ModeloDaMoto,
                c.Placa,
                c.DisponivelParaEntrega))
            .FirstOrDefaultAsync(ct);
}
