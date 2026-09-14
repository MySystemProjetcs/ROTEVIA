using DeliveryHub.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace DeliveryHub.Infrastructure.Identity;

internal sealed class GeradorDeLinkDeConvite : IGeradorDeLinkDeConvite
{
    private readonly string _baseUrl;

    public GeradorDeLinkDeConvite(IConfiguration configuration)
    {
        _baseUrl = configuration["Frontend:BaseUrl"]?.TrimEnd('/')
            ?? throw new InvalidOperationException("Frontend:BaseUrl não configurada.");
    }

    public string ConstruirUrl(Guid linkId, string token) => $"{_baseUrl}/convite/{linkId}?token={token}";
}
