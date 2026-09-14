using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Couriers;
using DeliveryHub.Domain.Merchants;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Application.Couriers;

// TokenClaro só existe neste retorno — nunca persistido, só o hash vai pro
// banco (mesmo espírito da SenhaProvisoria em CadastrarRestaurante).
public sealed record EntregadorConvidado(
    Guid CourierId, Guid LinkId, string TokenClaro, string ConviteUrl, bool EnviadoPeloWhatsApp);

public interface IConvidarEntregador
{
    Task<Result<EntregadorConvidado>> ExecutarAsync(
        Guid merchantId, string cpf, string nome, string telefone,
        string modeloDaMoto, string placa, string email, CancellationToken ct);
}

// Restaurante nunca define credencial de entregador (CLAUDE.md §6): aqui só se
// gera o convite. Quem cria a senha é o próprio motoboy em
// ConfirmarConviteEntregador.
public sealed class ConvidarEntregador : IConvidarEntregador
{
    // TTL do convite. 24h dá folga sem deixar um link velho pendurado por dias.
    private static readonly TimeSpan ValidadeDoConvite = TimeSpan.FromHours(24);

    private readonly ICourierRepository _couriers;
    private readonly IMerchantRepository _merchants;
    private readonly IGeradorDeConvite _geradorDeConvite;
    private readonly IGeradorDeLinkDeConvite _geradorDeLink;
    private readonly IWhatsAppWorkerClient _whatsApp;
    private readonly IPasswordHasher _hasher;
    private readonly TimeProvider _timeProvider;

    public ConvidarEntregador(
        ICourierRepository couriers, IMerchantRepository merchants, IGeradorDeConvite geradorDeConvite,
        IGeradorDeLinkDeConvite geradorDeLink, IWhatsAppWorkerClient whatsApp, IPasswordHasher hasher,
        TimeProvider timeProvider)
    {
        _couriers = couriers;
        _merchants = merchants;
        _geradorDeConvite = geradorDeConvite;
        _geradorDeLink = geradorDeLink;
        _whatsApp = whatsApp;
        _hasher = hasher;
        _timeProvider = timeProvider;
    }

    public async Task<Result<EntregadorConvidado>> ExecutarAsync(
        Guid merchantId, string cpf, string nome, string telefone,
        string modeloDaMoto, string placa, string email, CancellationToken ct)
    {
        var agora = _timeProvider.GetUtcNow();

        var courier = await _couriers.ObterPorCpfAsync(cpf, ct);
        if (courier is null)
        {
            courier = Courier.Criar(cpf, nome, telefone, modeloDaMoto, placa, agora);
            _couriers.Adicionar(courier);
        }
        else if (await _couriers.ExisteVinculoAtivoOuPendenteAsync(courier.Id, merchantId, ct))
        {
            return Result.Failure<EntregadorConvidado>(CourierErrors.EntregadorJaVinculado);
        }

        var tokenClaro = _geradorDeConvite.Gerar();
        var link = CourierMerchantLink.Convidar(
            courier.Id, merchantId, email, _hasher.Hash(tokenClaro), agora.Add(ValidadeDoConvite), agora);

        _couriers.AdicionarLink(link);
        await _couriers.SalvarAsync(ct);

        var conviteUrl = _geradorDeLink.ConstruirUrl(link.Id, tokenClaro);

        // O convite já está persistido e vale nesse ponto — uma falha aqui é
        // motivo pra avisar, não pra desfazer o cadastro. O link continua
        // disponível na tela pra compartilhar manualmente.
        var enviadoPeloWhatsApp = await TentarEnviarConviteAsync(merchantId, telefone, nome, conviteUrl, link.Id, ct);

        return Result.Success(new EntregadorConvidado(courier.Id, link.Id, tokenClaro, conviteUrl, enviadoPeloWhatsApp));
    }

    private async Task<bool> TentarEnviarConviteAsync(
        Guid merchantId, string telefone, string nome, string conviteUrl, Guid linkId, CancellationToken ct)
    {
        var texto = $"Você foi convidado para ser entregador no ROTEVIA. Toque no link para criar sua senha: {conviteUrl}";

        try
        {
            var enviada = await _whatsApp.EnviarMensagemAsync(merchantId, telefone, texto, linkId.ToString(), ct);
            if (enviada.ExternalId is null)
                return false;

            var merchant = await _merchants.ObterPorIdAsync(merchantId, ct);
            merchant?.RegistrarMensagemWhatsApp(new MensagemWhatsApp(
                DirecaoMensagemWhatsApp.Enviada, telefone, texto, enviada.ExternalId, _timeProvider.GetUtcNow()));

            if (merchant is not null)
                await _merchants.SalvarAsync(ct);

            return true;
        }
        catch (Exception)
        {
            // Sem logger aqui de propósito (Application não tem dependência
            // nenhuma de infraestrutura) — o worker/HttpClient já loga a
            // falha real do lado deles. Aqui só decide não derrubar o convite.
            return false;
        }
    }
}
