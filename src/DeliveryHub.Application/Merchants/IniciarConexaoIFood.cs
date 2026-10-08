using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.SharedKernel;
using System.Net;

namespace DeliveryHub.Application.Merchants;

public static class ConexaoIFoodErrors
{
    public static readonly Error MerchantNaoEncontrado = new(
        "merchant.nao_encontrado", "Restaurante não encontrado.", ErrorType.NotFound);

    public static readonly Error LojaJaVinculadaAOutroCadastro = new(
        "merchant.loja_ja_vinculada",
        "Essa loja do iFood já está vinculada a outro restaurante no sistema.",
        ErrorType.Conflict);

    public static readonly Error AutenticacaoIntegradaFalhou = new(
        "ifood.autenticacao_integrada_falhou",
        "HOUVE ERRO NA SUA AUTENTICAÇÃO INTEGRADA. ENTRE EM CONTATO COM O SUPORTE.",
        ErrorType.Failure);
}

public sealed record ConexaoIniciada(string UserCode, string VerificationUrlComplete, DateTimeOffset ExpiraEm);

public interface IIniciarConexaoIFood
{
    Task<Result<ConexaoIniciada>> ExecutarAsync(Guid merchantId, CancellationToken ct);
}

// Gera o código que o dono da loja leva até o Portal do Parceiro. Não é o
// dono do DeliveryHub autorizando por ele — é o restaurante se autoconectando.
public sealed class IniciarConexaoIFood : IIniciarConexaoIFood
{
    private readonly IMerchantRepository _merchants;
    private readonly IIFoodMerchantConnector _connector;

    public IniciarConexaoIFood(IMerchantRepository merchants, IIFoodMerchantConnector connector)
    {
        _merchants = merchants;
        _connector = connector;
    }

    public async Task<Result<ConexaoIniciada>> ExecutarAsync(Guid merchantId, CancellationToken ct)
    {
        var merchant = await _merchants.ObterPorIdAsync(merchantId, ct);
        if (merchant is null)
            return Result.Failure<ConexaoIniciada>(ConexaoIFoodErrors.MerchantNaoEncontrado);

        CodigoDeVinculoIFood codigo;
        try
        {
            codigo = await _connector.SolicitarCodigoAsync(ct);
        }
        catch (HttpRequestException ex) when (
            ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return Result.Failure<ConexaoIniciada>(ConexaoIFoodErrors.AutenticacaoIntegradaFalhou);
        }

        var resultado = merchant.IniciarConexaoIFood(codigo.UserCode, codigo.AuthorizationCodeVerifier, codigo.ExpiraEm);
        if (resultado.IsFailure)
            return Result.Failure<ConexaoIniciada>(resultado.Error);

        await _merchants.SalvarAsync(ct);

        return Result.Success(new ConexaoIniciada(codigo.UserCode, codigo.VerificationUrlComplete, codigo.ExpiraEm));
    }
}
