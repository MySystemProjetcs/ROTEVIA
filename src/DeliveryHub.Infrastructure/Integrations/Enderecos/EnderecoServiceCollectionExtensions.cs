using System.Net.Http.Headers;
using DeliveryHub.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeliveryHub.Infrastructure.Integrations.Enderecos;

public static class EnderecoServiceCollectionExtensions
{
    public static IServiceCollection AddBuscaDeEndereco(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GoogleMapsOptions>(configuration.GetSection(GoogleMapsOptions.SectionName));

        services.AddHttpClient<IResolverEndereco, ResolverEnderecoHttp>(http =>
        {
            // O Nominatim exige User-Agent identificando a aplicação e recusa
            // quem não manda (política de uso do OpenStreetMap).
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ROTEVIA", "1.0"));

            // Serviço de terceiro no caminho de um formulário: melhor desistir
            // rápido e deixar o lojista digitar do que travar a tela.
            http.Timeout = TimeSpan.FromSeconds(8);
        });

        return services;
    }
}
