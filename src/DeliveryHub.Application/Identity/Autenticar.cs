using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Identity;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Application.Identity;

public static class AutenticacaoErrors
{
    // Mensagem única de propósito: distinguir "e-mail não existe" de "senha
    // errada" entrega ao atacante a lista de quem tem conta no sistema.
    public static readonly Error CredenciaisInvalidas = new(
        "auth.credenciais_invalidas",
        "E-mail ou senha inválidos.",
        ErrorType.Unauthorized);

    public static readonly Error SemLojaVinculada = new(
        "auth.sem_loja_vinculada",
        "Usuário não possui loja vinculada.",
        ErrorType.Conflict);
}

public sealed record ResultadoLogin(string AccessToken, DateTimeOffset ExpiraEm, bool DeveTrocarSenha, string? NomeRestaurante, string NomeUsuario);

public interface IAutenticar
{
    Task<Result<ResultadoLogin>> ExecutarAsync(string email, string senha, CancellationToken ct);
}

public sealed class Autenticar : IAutenticar
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IMerchantRepository _merchants;
    private readonly IPasswordHasher _hasher;
    private readonly IGeradorDeToken _token;

    public Autenticar(IUsuarioRepository usuarios, IMerchantRepository merchants, IPasswordHasher hasher, IGeradorDeToken token)
    {
        _usuarios = usuarios;
        _merchants = merchants;
        _hasher = hasher;
        _token = token;
    }

    public async Task<Result<ResultadoLogin>> ExecutarAsync(string email, string senha, CancellationToken ct)
    {
        var usuario = await _usuarios.ObterPorEmailAsync(email, ct);

        if (usuario is null || !usuario.Ativo || !_hasher.Verificar(senha, usuario.SenhaHash))
            return Result.Failure<ResultadoLogin>(AutenticacaoErrors.CredenciaisInvalidas);

        Guid? merchantId = null;

        if (usuario.Papel == PapelUsuario.DonoRestaurante)
        {
            // Hoje é uma loja por dono. Quando passar a ser várias, o que muda
            // é só a escolha aqui — o token já carrega a loja ativa.
            merchantId = await _usuarios.ObterMerchantVinculadoAsync(usuario.Id, ct);

            if (merchantId is null)
                return Result.Failure<ResultadoLogin>(AutenticacaoErrors.SemLojaVinculada);
        }

        string? nomeRestaurante = null;
        if (merchantId is not null)
        {
            var merchant = await _merchants.ObterPorIdAsync(merchantId.Value, ct);
            nomeRestaurante = merchant?.Nome;
        }

        var emitido = _token.Gerar(usuario, merchantId);

        return Result.Success(new ResultadoLogin(emitido.AccessToken, emitido.ExpiraEm, usuario.DeveTrocarSenha, nomeRestaurante, usuario.Nome));
    }
}
