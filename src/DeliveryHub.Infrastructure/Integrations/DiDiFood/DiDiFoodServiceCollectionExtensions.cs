using System.Net.Http.Headers;
using DeliveryHub.Application.Abstractions;
using DeliveryHub.Infrastructure.Integrations.DiDiFood.Orders;
using DeliveryHub.Infrastructure.Integrations.DiDiFood.Polling;
using DeliveryHub.Infrastructure.Integrations.IFood;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DeliveryHub.Infrastructure.Integrations.DiDiFood;

public static class DiDiFoodServiceCollectionExtensions
{
    private const string DiDiFoodBaseAddress = "https://openapi.didi-food.com/";

    public static IServiceCollection AddDiDiFoodIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<DiDiFoodOptions>()
            .Bind(configuration.GetSection(DiDiFoodOptions.SectionName))
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IDiDiFoodInboxProcessor, DiDiFoodInboxProcessor>();
        services.AddScoped<IFoodOrderSource>();
        services.AddScoped<DiDiFoodOrderSource>();
        services.AddScoped<IOrderSource, OrderSourceResolver>();

        services.AddHttpClient<IDiDiFoodOrderClient, DiDiFoodOrderClient>(client =>
        {
            client.BaseAddress = new Uri(DiDiFoodBaseAddress);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        return services;
    }
}
