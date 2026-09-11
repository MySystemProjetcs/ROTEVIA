using DeliveryHub.Domain.Merchants;
using DeliveryHub.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace DeliveryHub.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<IntegrationInboxEvent> IntegrationInbox => Set<IntegrationInboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Dinheiro nunca em ponto flutuante.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}
