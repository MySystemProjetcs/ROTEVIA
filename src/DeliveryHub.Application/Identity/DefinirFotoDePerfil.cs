using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Identity;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Application.Identity;

public interface IDefinirFotoDePerfil
{
    Task<Result> ExecutarAsync(Guid usuarioId, string fotoBase64, CancellationToken ct);
}

// A foto é sempre a do próprio usuário da sessão: o id vem do token, nunca do
// corpo da requisição — senão qualquer um trocaria a foto de qualquer outro.
public sealed class DefinirFotoDePerfil : IDefinirFotoDePerfil
{
    private readonly IUsuarioRepository _usuarios;

    public DefinirFotoDePerfil(IUsuarioRepository usuarios)
    {
        _usuarios = usuarios;
    }

    public async Task<Result> ExecutarAsync(Guid usuarioId, string fotoBase64, CancellationToken ct)
    {
        var usuario = await _usuarios.ObterPorIdAsync(usuarioId, ct);
        if (usuario is null)
            return Result.Failure(AutenticacaoErrors.CredenciaisInvalidas);

        var definida = usuario.DefinirFoto(fotoBase64);
        if (definida.IsFailure)
            return definida;

        await _usuarios.SalvarAsync(ct);

        return Result.Success();
    }
}
