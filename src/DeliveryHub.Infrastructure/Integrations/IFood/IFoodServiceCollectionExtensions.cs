using System.Net;
using System.Net.Http.Headers;
using DeliveryHub.Application.Abstractions;
using DeliveryHub.Infrastructure.Integrations.IFood.Auth;
using DeliveryHub.Infrastructure.Integrations.IFood.Merchants;
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
    private const string MerchantBaseAddress = "https://merchant-api.ifood.com.br/merchant/v1.0/";

    public static IServiceCollection AddIFoodIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<IFoodOptions>()
            .Bind(configuration.GetSection(IFoodOptions.SectionName))
            .ValidateOnStart();

        services
            .AddOptions<IFoodDistributedOptions>()
            .Bind(configuration.GetSection(IFoodDistributedOptions.SectionName))
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        // Singleton: o cache do token mora na instância. Como transient, toda
        // injeção começaria sem token e reautenticaria — caminho para bloqueio.
        services.AddSingleton<IIFoodAuthenticator, IFoodAuthenticator>();
        services.AddTransient<IFoodAuthorizationHandler>();

        // Singleton: a métrica precisa sobreviver entre ciclos do worker.
        services.AddSingleton<IPollingHealth, PollingHealth>();
        services.AddScoped<IIFoodEventIngestor, IFoodEventIngestor>();
        services.AddScoped<IIFoodInboxProcessor, IFoodInboxProcessor>();
        services.AddScoped<IOrderSource, IFoodOrderSource>();

        // Sem estado a manter entre chamadas — cada troca de token já devolve
        // seu próprio expiresIn, então não precisa ser singleton como o
        // autenticador Centralizado.
        services.AddScoped<IIFoodMerchantConnector, IFoodMerchantConnector>();
        services.AddScoped<IIFoodMerchantTokenProvider, IFoodMerchantTokenProvider>();

        services
            .AddHttpClient(IFoodHttpClients.Authentication, client => Configure(client, AuthenticationBaseAddress))
            .ConfigurePrimaryHttpMessageHandler(PrimaryHandler);

        services
            .AddHttpClient(IFoodHttpClients.AuthenticationDistributed, client => Configure(client, AuthenticationBaseAddress))
            .ConfigurePrimaryHttpMessageHandler(PrimaryHandler);

        services
            .AddHttpClient(IFoodHttpClients.MerchantDistributed, client => Configure(client, MerchantBaseAddress))
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
