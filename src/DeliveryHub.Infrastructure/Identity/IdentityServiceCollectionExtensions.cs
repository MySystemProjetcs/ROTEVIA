using DeliveryHub.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeliveryHub.Infrastructure.Identity;

public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IGeradorDeSenha, GeradorDeSenha>();
        services.AddSingleton<IGeradorDeConvite, GeradorDeConvite>();
        services.AddScoped<IGeradorDeToken, GeradorDeToken>();
        services.AddScoped<IGeradorDeLinkDeConvite, GeradorDeLinkDeConvite>();

        return services;
    }
}
