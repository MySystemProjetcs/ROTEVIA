using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Application.Merchants;

public interface IConfirmarConexaoIFood
{
    Task<Result> ExecutarAsync(Guid merchantId, string authorizationCode, CancellationToken ct);
}

public sealed class ConfirmarConexaoIFood : IConfirmarConexaoIFood
{
    private readonly IMerchantRepository _merchants;
    private readonly IIFoodMerchantConnector _connector;
    private readonly TimeProvider _timeProvider;

    public ConfirmarConexaoIFood(IMerchantRepository merchants, IIFoodMerchantConnector connector, TimeProvider timeProvider)
    {
        _merchants = merchants;
        _connector = connector;
        _timeProvider = timeProvider;
    }

    public async Task<Result> ExecutarAsync(Guid merchantId, string authorizationCode, CancellationToken ct)
    {
        var merchant = await _merchants.ObterPorIdAsync(merchantId, ct);
        if (merchant is null)
            return Result.Failure(ConexaoIFoodErrors.MerchantNaoEncontrado);

        // O verifier vive só no nosso lado, pareado ao userCode que geramos —
        // é o que impede alguém de trocar um authorizationCode alheio pelo token.
        var verifier = merchant.ConexaoIFood?.AuthorizationCodeVerifier;
        if (verifier is null)
            return Result.Failure(DeliveryHub.Domain.Merchants.MerchantErrors.ConexaoNaoIniciada);

        var token = await _connector.TrocarPorTokenAsync(authorizationCode, verifier, ct);
        var ifoodMerchantId = await _connector.DescobrirMerchantIdAsync(token.AccessToken, ct);

        // A troca com o iFood já aconteceu neste ponto — não dá para desfazer.
        // Ainda assim, não gravar em cima de outro restaurante já dono dessa
        // loja é a última rede de segurança contra vínculo duplicado.
        if (await _merchants.ExistePorIFoodIdAsync(ifoodMerchantId, merchantId, ct))
            return Result.Failure(ConexaoIFoodErrors.LojaJaVinculadaAOutroCadastro);

        var agora = _timeProvider.GetUtcNow();
        var resultado = merchant.ConfirmarConexaoIFood(
            ifoodMerchantId, token.AccessToken, token.RefreshToken, token.Type, token.ExpiraEm, agora);

        if (resultado.IsFailure)
            return resultado;

        await _merchants.SalvarAsync(ct);
        return Result.Success();
    }
}
