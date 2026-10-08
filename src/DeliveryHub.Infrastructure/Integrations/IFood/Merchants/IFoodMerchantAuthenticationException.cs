namespace DeliveryHub.Infrastructure.Integrations.IFood.Merchants;

internal sealed class IFoodMerchantAuthenticationException : Exception
{
    public IFoodMerchantAuthenticationException(HttpRequestException innerException)
        : base("A autenticação integrada do iFood foi recusada.", innerException)
    {
    }
}