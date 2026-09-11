using System.Net;

namespace DeliveryHub.Infrastructure.Integrations.IFood;

// Falha vinda da API do iFood já com o corpo de erro interpretado.
// UnauthorizedMerchants só vem preenchido no 403 do polling, quando o iFood
// diz exatamente quais lojas perderam a autorização.
public sealed class IFoodApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? IFoodErrorCode { get; }
    public IReadOnlyList<Guid> UnauthorizedMerchants { get; }

    public IFoodApiException(
        HttpStatusCode statusCode,
        string message,
        string? ifoodErrorCode = null,
        IReadOnlyList<Guid>? unauthorizedMerchants = null) : base(message)
    {
        StatusCode = statusCode;
        IFoodErrorCode = ifoodErrorCode;
        UnauthorizedMerchants = unauthorizedMerchants ?? [];
    }
}
