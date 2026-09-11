using System.Net;
using System.Net.Http.Headers;
using DeliveryHub.Infrastructure.Integrations.IFood.Auth;
using DeliveryHub.Infrastructure.Integrations.IFood.Orders;
using DeliveryHub.Infrastructure.Integrations.IFood.Polling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DeliveryHub.Infrastructure.Integrations.IFood;

public static class IFoodServiceCollectionExtensions
{
    private const string AuthenticationBaseAddress = "https://merchant-api.ifood.com.br/authentication/v1.0/";
    private const string EventsBaseAddress = "https://merchant-api.ifood.com.br/events/v1.0/";
    private const string OrderBaseAddress = "https://merchant-api.ifood.com.br/order/v1.0/";

    public static IServiceCollection AddIFoodIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<IFoodOptions>()
            .Bind(configuration.GetSection(IFoodOptions.SectionName))
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        // Singleton: o cache do token mora na instância. Como transient, toda
        // injeção começaria sem token e reautenticaria — caminho para bloqueio.
        services.AddSingleton<IIFoodAuthenticator, IFoodAuthenticator>();
        services.AddTransient<IFoodAuthorizationHandler>();

        services
            .AddHttpClient(IFoodHttpClients.Authentication, client => Configure(client, AuthenticationBaseAddress))
            .ConfigurePrimaryHttpMessageHandler(PrimaryHandler);

        services
            .AddHttpClient<IIFoodEventsClient, IFoodEventsClient>(client => Configure(client, EventsBaseAddress))
            .ConfigurePrimaryHttpMessageHandler(PrimaryHandler)
            .AddHttpMessageHandler<IFoodAuthorizationHandler>();

        services
            .AddHttpClient<IIFoodOrderClient, IFoodOrderClient>(client => Configure(client, OrderBaseAddress))
            .ConfigurePrimaryHttpMessageHandler(PrimaryHandler)
            .AddHttpMessageHandler<IFoodAuthorizationHandler>();

        return services;
    }

    private static void Configure(HttpClient client, string baseAddress)
    {
        client.BaseAddress = new Uri(baseAddress);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    // O gateway do iFood responde gzip mesmo sem Accept-Encoding na requisição;
    // sem isto o corpo chega comprimido e a desserialização quebra.
    private static HttpMessageHandler PrimaryHandler() => new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.All
    };
}
