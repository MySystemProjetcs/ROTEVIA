using DeliveryHub.Application.Abstractions;
using DeliveryHub.Application.Orders;
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
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IIntegrationInboxWriter, IntegrationInboxWriter>();
        services.AddScoped<IMerchantResolver, MerchantResolver>();
        services.AddScoped<IPedidoRepository, PedidoRepository>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IMerchantRepository, MerchantRepository>();
        services.AddScoped<IListarPedidos, ListarPedidosQuery>();

        return services;
    }
}
