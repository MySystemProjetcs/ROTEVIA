using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Identity;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Application.Identity;

public interface IAlterarEmailDoUsuario
{
    Task<Result> ExecutarAsync(Guid usuarioId, string novoEmail, CancellationToken ct);
}

// Troca o e-mail de login da própria conta. Vale para o sistema inteiro: é por
// ele que a pessoa entra depois, e é ele que o perfil mostra.
//
// Sempre sobre o usuário da sessão — nunca recebe um id pela URL, senão viraria
// porta para trocar o e-mail (e o login) de outra conta.
public sealed class AlterarEmailDoUsuario : IAlterarEmailDoUsuario
{
    private readonly IUsuarioRepository _usuarios;

    public AlterarEmailDoUsuario(IUsuarioRepository usuarios)
    {
        _usuarios = usuarios;
    }

    public async Task<Result> ExecutarAsync(Guid usuarioId, string novoEmail, CancellationToken ct)
    {
        var usuario = await _usuarios.ObterPorIdAsync(usuarioId, ct);
        if (usuario is null)
            return Result.Failure(UsuarioErrors.EmailInvalido);

        var normalizado = novoEmail?.Trim().ToLowerInvariant() ?? string.Empty;

        // Reenviar o mesmo e-mail é sucesso silencioso: o usuário salvou sem
        // mudar nada, e falhar aqui seria ruído.
        if (normalizado == usuario.Email)
            return Result.Success();

        // Checagem antes do índice único só para devolver mensagem boa; quem
        // garante de verdade é ux_usuarios_email no banco.
        if (await _usuarios.ExisteComEmailAsync(normalizado, ct))
            return Result.Failure(UsuarioErrors.EmailEmUso);

        var resultado = usuario.AlterarEmail(normalizado);
        if (resultado.IsFailure)
            return resultado;

        await _usuarios.SalvarAsync(ct);
        return Result.Success();
    }
}
