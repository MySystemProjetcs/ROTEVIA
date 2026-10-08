using System.Net.Http.Headers;
using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Merchants;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Merchants;

internal sealed class IFoodMerchantTokenProvider : IIFoodMerchantTokenProvider
{
    // Margem curta, não percentual: a doc de homologação do Order/Events pede
    // "renove apenas quando expirado, não antecipadamente" — diferente da
    // margem de 90% do autenticador Centralizado, que não passa por
    // homologação nesses termos. Só o suficiente para não perder a corrida
    // entre "token ainda válido" e "requisição chegando com ele expirado".
    private static readonly TimeSpan MargemDeRenovacao = TimeSpan.FromSeconds(60);

    private readonly IIFoodMerchantConnector _connector;
    private readonly IMerchantRepository _merchants;
    private readonly TimeProvider _timeProvider;

    public IFoodMerchantTokenProvider(
        IIFoodMerchantConnector connector, IMerchantRepository merchants, TimeProvider timeProvider)
    {
        _connector = connector;
        _merchants = merchants;
        _timeProvider = timeProvider;
    }

    public async Task<AuthenticationHeaderValue> ObterAsync(Merchant merchant, CancellationToken ct)
    {
        var conexao = merchant.ConexaoIFood
            ?? throw new InvalidOperationException($"Merchant {merchant.Id} sem conexão iFood — não deveria ter chegado aqui.");

        var agora = _timeProvider.GetUtcNow();

        if (conexao.TokenExpiraEm - agora > MargemDeRenovacao)
            return new AuthenticationHeaderValue(conexao.TipoToken ?? "bearer", conexao.AccessToken);

        TokenDistribuidoIFood renovado;
        try
        {
            renovado = await _connector.RenovarTokenAsync(conexao.RefreshToken!, ct);
        }
        catch (HttpRequestException ex) when (
            ex.StatusCode is System.Net.HttpStatusCode.BadRequest
                or System.Net.HttpStatusCode.Unauthorized
                or System.Net.HttpStatusCode.Forbidden)
        {
            throw new IFoodMerchantAuthenticationException(ex);
        }

        merchant.AtualizarTokens(renovado.AccessToken, renovado.RefreshToken, renovado.Type, renovado.ExpiraEm);
        await _merchants.SalvarAsync(ct);

        return new AuthenticationHeaderValue(renovado.Type, renovado.AccessToken);
    }
}
