using System.Net;
using System.Net.Http.Headers;
using DeliveryHub.Infrastructure.Integrations.IFood.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DeliveryHub.Infrastructure.Integrations.IFood;

public static class IFoodServiceCollectionExtensions
{
    private static readonly Uri AuthenticationBaseAddress = new("https://merchant-api.ifood.com.br/authentication/v1.0/");

    public static IServiceCollection AddIFoodIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<IFoodOptions>()
            .Bind(configuration.GetSection(IFoodOptions.SectionName))
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        services
            .AddHttpClient<IIFoodAuthenticator, IFoodAuthenticator>(client =>
            {
                client.BaseAddress = AuthenticationBaseAddress;
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            // O gateway do iFood responde gzip mesmo sem Accept-Encoding na requisição;
            // sem isto o corpo chega comprimido e a desserialização quebra.
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.All
            });

        return services;
    }
}
