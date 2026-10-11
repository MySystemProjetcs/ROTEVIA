using DeliveryHub.Application.Abstractions;
using DeliveryHub.Application.Couriers;
using DeliveryHub.Application.Dashboard;
using DeliveryHub.Application.Identity;
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

        // NÃO usar AddDbContextPool aqui: AppDbContext captura ITenantContext
        // via closure dentro dos HasQueryFilter do OnModelCreating. O pool
        // roda OnModelCreating uma única vez e cacheia o model — o tenant
        // da primeira request grudaria em todas as outras, vazando dados
        // entre lojas. Pra migrar pra pool depois, HasQueryFilter precisa
        // ler o tenant via scope capturado em runtime (ex.: _tenant resolvido
        // por IServiceProvider interno a cada query), não como campo do ctor.
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IIntegrationInboxWriter, IntegrationInboxWriter>();
        services.AddScoped<IMerchantResolver, MerchantResolver>();
        services.AddScoped<IPedidoRepository, PedidoRepository>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IMerchantRepository, MerchantRepository>();
        services.AddScoped<IIFoodMerchantSnapshotStore, EfIFoodMerchantSnapshotStore>();
        services.AddScoped<ICourierRepository, CourierRepository>();
        services.AddScoped<IRastreioRepository, RastreioRepository>();
        services.AddScoped<IListarPedidos, ListarPedidosQuery>();
        services.AddScoped<IListarMinhasEntregas, ListarMinhasEntregasQuery>();
        services.AddScoped<IObterResumoDashboard, ResumoDashboardQuery>();
        services.AddScoped<IObterPerfil, PerfilQuery>();
        services.AddScoped<IObterEnderecoDaLoja, ObterEnderecoDaLojaQuery>();
        services.AddScoped<IObterGanhosEntregador, GanhosEntregadorQuery>();
        services.AddScoped<IRegistrarPosicao, RegistrarPosicao>();
        services.AddScoped<IImpressoraRepository, ImpressoraRepository>();

        return services;
    }
}
