using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Couriers;
using DeliveryHub.Domain.Identity;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Application.Couriers;

public interface IConfirmarConviteEntregador
{
    Task<Result> ExecutarAsync(Guid linkId, string token, string senha, CancellationToken ct);
}

// Sem autenticação: quem chama é o motoboy, que ainda não tem conta. O token
// do convite é a única prova de identidade aqui — por isso é comparado por
// hash, igual senha, e tem TTL.
public sealed class ConfirmarConviteEntregador : IConfirmarConviteEntregador
{
    private readonly ICourierRepository _couriers;
    private readonly IUsuarioRepository _usuarios;
    private readonly IPasswordHasher _hasher;
    private readonly TimeProvider _timeProvider;

    public ConfirmarConviteEntregador(
        ICourierRepository couriers, IUsuarioRepository usuarios,
        IPasswordHasher hasher, TimeProvider timeProvider)
    {
        _couriers = couriers;
        _usuarios = usuarios;
        _hasher = hasher;
        _timeProvider = timeProvider;
    }

    public async Task<Result> ExecutarAsync(Guid linkId, string token, string senha, CancellationToken ct)
    {
        var link = await _couriers.ObterLinkAsync(linkId, ct);
        if (link is null)
            return Result.Failure(CourierErrors.VinculoNaoEncontrado);

        if (link.TokenConviteHash is null || !_hasher.Verificar(token, link.TokenConviteHash))
            return Result.Failure(CourierErrors.ConviteInvalido);

        var agora = _timeProvider.GetUtcNow();
        var ativacao = link.Ativar(agora);
        if (ativacao.IsFailure)
            return ativacao;

        var courier = await _couriers.ObterPorIdAsync(link.CourierId, ct)
            ?? throw new InvalidOperationException("CourierMerchantLink aponta para Courier inexistente.");

        // Um motoboy convidado por um segundo restaurante já tem Usuario de um
        // convite anterior: reusa a conta em vez de pedir senha de novo.
        if (courier.UsuarioId is null)
        {
            var usuario = Usuario.Criar(link.Email, _hasher.Hash(senha), courier.Nome, PapelUsuario.Entregador, agora);
            _usuarios.Adicionar(usuario);
            courier.VincularUsuario(usuario.Id);
        }

        await _couriers.SalvarAsync(ct);
        return Result.Success();
    }
}
