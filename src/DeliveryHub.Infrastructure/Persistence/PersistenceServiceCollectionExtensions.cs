using DeliveryHub.Application.Abstractions;
using DeliveryHub.Application.Couriers;
using DeliveryHub.Application.Dashboard;
using DeliveryHub.Application.Merchants;
using DeliveryHub.Application.Orders;
using DeliveryHub.Application.Tracking;
using DeliveryHub.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DeliveryHub.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default não configurada.");

        services.TryAddSingleton(TimeProvider.System);

        services
            .AddOptions<CredentialCipherOptions>()
            .Bind(configuration.GetSection(CredentialCipherOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<ICredentialCipher, AesGcmCredentialCipher>();

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IIntegrationInboxWriter, IntegrationInboxWriter>();
        services.AddScoped<IMerchantResolver, MerchantResolver>();
        services.AddScoped<IPedidoRepository, PedidoRepository>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IMerchantRepository, MerchantRepository>();
        services.AddScoped<ICourierRepository, CourierRepository>();
        services.AddScoped<IRastreioRepository, RastreioRepository>();
        services.AddScoped<IListarPedidos, ListarPedidosQuery>();
        services.AddScoped<IListarMinhasEntregas, ListarMinhasEntregasQuery>();
        services.AddScoped<IObterResumoDashboard, ResumoDashboardQuery>();
        services.AddScoped<IObterEnderecoDaLoja, ObterEnderecoDaLojaQuery>();
        services.AddScoped<IObterGanhosEntregador, GanhosEntregadorQuery>();
        services.AddScoped<IRegistrarPosicao, RegistrarPosicao>();

        return services;
    }
}
