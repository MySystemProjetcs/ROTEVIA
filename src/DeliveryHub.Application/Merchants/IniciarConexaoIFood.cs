using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Application.Merchants;

public static class ConexaoIFoodErrors
{
    public static readonly Error MerchantNaoEncontrado = new(
        "merchant.nao_encontrado", "Restaurante não encontrado.", ErrorType.NotFound);

    public static readonly Error LojaJaVinculadaAOutroCadastro = new(
        "merchant.loja_ja_vinculada",
        "Essa loja do iFood já está vinculada a outro restaurante no sistema.",
        ErrorType.Conflict);
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

        var codigo = await _connector.SolicitarCodigoAsync(ct);

        var resultado = merchant.IniciarConexaoIFood(codigo.UserCode, codigo.AuthorizationCodeVerifier, codigo.ExpiraEm);
        if (resultado.IsFailure)
            return Result.Failure<ConexaoIniciada>(resultado.Error);

        await _merchants.SalvarAsync(ct);

        return Result.Success(new ConexaoIniciada(codigo.UserCode, codigo.VerificationUrlComplete, codigo.ExpiraEm));
    }
}
