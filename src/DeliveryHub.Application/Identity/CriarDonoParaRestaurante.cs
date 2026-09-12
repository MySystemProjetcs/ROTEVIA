using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Identity;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Application.Identity;

public sealed record DonoCriado(Guid UsuarioId, string SenhaProvisoria);

public interface ICriarDonoParaRestaurante
{
    Task<Result<DonoCriado>> ExecutarAsync(
        Guid merchantId, string email, string nome, CancellationToken ct);
}

// Separado de CadastrarRestaurante porque a loja pode existir antes do dono —
// foi o caso da loja de teste, cadastrada antes de haver identidade — e porque
// troca de dono é operação normal.
public sealed class CriarDonoParaRestaurante : ICriarDonoParaRestaurante
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IPasswordHasher _hasher;
    private readonly IGeradorDeSenha _geradorDeSenha;
    private readonly TimeProvider _timeProvider;

    public CriarDonoParaRestaurante(
        IUsuarioRepository usuarios,
        IPasswordHasher hasher,
        IGeradorDeSenha geradorDeSenha,
        TimeProvider timeProvider)
    {
        _usuarios = usuarios;
        _hasher = hasher;
        _geradorDeSenha = geradorDeSenha;
        _timeProvider = timeProvider;
    }

    public async Task<Result<DonoCriado>> ExecutarAsync(
        Guid merchantId, string email, string nome, CancellationToken ct)
    {
        if (await _usuarios.ExisteComEmailAsync(email, ct))
            return Result.Failure<DonoCriado>(CadastroErrors.EmailJaUsado);

        var agora = _timeProvider.GetUtcNow();
        var senhaProvisoria = _geradorDeSenha.Gerar();

        var usuario = Usuario.Criar(
            email, _hasher.Hash(senhaProvisoria), nome, PapelUsuario.DonoRestaurante, agora);

        _usuarios.Adicionar(usuario);
        _usuarios.Vincular(UsuarioMerchant.Criar(usuario.Id, merchantId, agora));

        await _usuarios.SalvarAsync(ct);

        return Result.Success(new DonoCriado(usuario.Id, senhaProvisoria));
    }
}
