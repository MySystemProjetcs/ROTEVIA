using System.Net.Http.Headers;
using DeliveryHub.Application.Abstractions;
using DeliveryHub.Infrastructure.Integrations.DiDiFood.Auth;
using DeliveryHub.Infrastructure.Integrations.DiDiFood.Catalog;
using DeliveryHub.Infrastructure.Integrations.DiDiFood.Orders;
using DeliveryHub.Infrastructure.Integrations.DiDiFood.Polling;
using DeliveryHub.Infrastructure.Integrations.IFood;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DeliveryHub.Infrastructure.Integrations.DiDiFood;

// Registro exclusivo da 99Food. Segregação total do iFood: nenhuma linha
// aqui toca em tipo do módulo IFood além do self-bind de IFoodOrderSource que
// o OrderSourceResolver precisa injetar — e esse self-bind vive dentro de
// AddIFoodIntegration, não aqui (é o próprio módulo do iFood que se registra).
public static class DiDiFoodServiceCollectionExtensions
{
    // "A base URL sempre será https://openapi.99food.com" — guia de
    // integração, seção "Entendendo Funcionalidades". O host antigo
    // (openapi.didi-food.com) que estava aqui não bate com a documentação
    // oficial e precisa ser confirmado/descartado na validação viva.
    private const string DiDiFoodBaseAddress = "https://openapi.99food.com/";

    public static IServiceCollection AddDiDiFoodIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        // ValidateDataAnnotations ativa os atributos [Required]/[Range] em
        // DiDiFoodOptions; sem isso ValidateOnStart não dispara nenhuma
        // verificação e um AppSecret vazio em produção só quebraria na
        // primeira assinatura MD5 do webhook — tarde demais.
        services
            .AddOptions<DiDiFoodOptions>()
            .Bind(configuration.GetSection(DiDiFoodOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IDiDiFoodInboxProcessor, DiDiFoodInboxProcessor>();
        services.AddScoped<INoventaENoveMerchantResolver, NoventaENoveMerchantResolver>();
        services.AddScoped<IDiDiFoodWebhookGateway, DiDiFoodWebhookGateway>();
        services.AddScoped<IDiDiFoodAuthenticator, DiDiFoodAuthenticator>();
        services.AddScoped<IDiDiFoodCatalogClient, DiDiFoodCatalogClient>();
        services.AddScoped<DiDiFoodOrderSource>();
        services.AddScoped<IOrderSource, OrderSourceResolver>();

        services.AddHttpClient<IDiDiFoodOrderClient, DiDiFoodOrderClient>(client =>
        {
            client.BaseAddress = new Uri(DiDiFoodBaseAddress);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        // Cliente nomeado, não tipado: o autenticador monta a URI com
        // app_id/app_secret/app_shop_id na query, não precisa de um client
        // dedicado por método como o de pedidos.
        services.AddHttpClient(nameof(DiDiFoodAuthenticator), client =>
        {
            client.BaseAddress = new Uri(DiDiFoodBaseAddress);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        // Catálogo também é nomeado: o auth_token vai na query (v3/item/*),
        // não em header, então não há handler de autorização a encaixar.
        services.AddHttpClient(nameof(DiDiFoodCatalogClient), client =>
        {
            client.BaseAddress = new Uri(DiDiFoodBaseAddress);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        return services;
    }
}
