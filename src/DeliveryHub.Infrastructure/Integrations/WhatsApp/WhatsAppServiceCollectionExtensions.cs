using DeliveryHub.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DeliveryHub.Infrastructure.Integrations.WhatsApp;

public static class WhatsAppServiceCollectionExtensions
{
    public static IServiceCollection AddWhatsAppIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<WhatsAppOptions>()
            .Bind(configuration.GetSection(WhatsAppOptions.SectionName));

        services
            .AddHttpClient<IWhatsAppWorkerClient, WhatsAppWorkerClient>((sp, client) =>
            {
                var opcoes = sp.GetRequiredService<IOptions<WhatsAppOptions>>().Value;
                client.BaseAddress = new Uri(opcoes.WorkerBaseUrl);
            });

        return services;
    }
}
