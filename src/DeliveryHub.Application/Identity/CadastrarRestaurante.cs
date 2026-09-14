using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Identity;
using DeliveryHub.Domain.Merchants;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Application.Identity;

public static class CadastroErrors
{
    public static readonly Error EmailJaUsado = new(
        "cadastro.email_ja_usado",
        "Já existe usuário com este e-mail.",
        ErrorType.Conflict);
}

// A senha em claro aparece uma única vez, aqui, para o administrador repassar
// ao dono. Não é persistida e não pode ir para log.
public sealed record RestauranteCadastrado(Guid MerchantId, Guid UsuarioId, string SenhaProvisoria);

public interface ICadastrarRestaurante
{
    // Sem ifoodMerchantId de propósito: no fluxo Distribuído esse dado só
    // existe depois que o próprio dono autoriza a loja pelo Portal do
    // Parceiro — pedir aqui seria inventar informação que ainda não existe
    // no momento do cadastro.
    Task<Result<RestauranteCadastrado>> ExecutarAsync(string nomeLoja, string emailDono, string nomeDono, CancellationToken ct);
}

public sealed class CadastrarRestaurante : ICadastrarRestaurante
{
    private readonly IMerchantRepository _merchants;
    private readonly IUsuarioRepository _usuarios;
    private readonly IPasswordHasher _hasher;
    private readonly IGeradorDeSenha _geradorDeSenha;
    private readonly TimeProvider _timeProvider;

    public CadastrarRestaurante(
        IMerchantRepository merchants,
        IUsuarioRepository usuarios,
        IPasswordHasher hasher,
        IGeradorDeSenha geradorDeSenha,
        TimeProvider timeProvider)
    {
        _merchants = merchants;
        _usuarios = usuarios;
        _hasher = hasher;
        _geradorDeSenha = geradorDeSenha;
        _timeProvider = timeProvider;
    }

    public async Task<Result<RestauranteCadastrado>> ExecutarAsync(
        string nomeLoja, string emailDono, string nomeDono, CancellationToken ct)
    {
        if (await _usuarios.ExisteComEmailAsync(emailDono, ct))
            return Result.Failure<RestauranteCadastrado>(CadastroErrors.EmailJaUsado);

        var agora = _timeProvider.GetUtcNow();

        var merchant = Merchant.Criar(nomeLoja, agora);
        _merchants.Adicionar(merchant);

        var senhaProvisoria = _geradorDeSenha.Gerar();
        var usuario = Usuario.Criar(
            emailDono, _hasher.Hash(senhaProvisoria), nomeDono, PapelUsuario.DonoRestaurante, agora);

        _usuarios.Adicionar(usuario);
        _usuarios.Vincular(UsuarioMerchant.Criar(usuario.Id, merchant.Id, agora));

        await _usuarios.SalvarAsync(ct);

        return Result.Success(new RestauranteCadastrado(merchant.Id, usuario.Id, senhaProvisoria));
    }
}
