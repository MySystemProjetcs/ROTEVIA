using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Identity;
using DeliveryHub.Domain.Merchants;
using DeliveryHub.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace DeliveryHub.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    private readonly ITenantContext _tenant;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant) : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<UsuarioMerchant> UsuarioMerchants => Set<UsuarioMerchant>();
    public DbSet<IntegrationInboxEvent> IntegrationInbox => Set<IntegrationInboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Camada 1 do isolamento (CLAUDE.md §6): o filtro é estrutural, não
        // depende de ninguém lembrar de escrever Where(x => x.MerchantId == ...).
        // Requisição sem tenant e sem permissão ampla não enxerga nada.
        modelBuilder.Entity<Pedido>().HasQueryFilter(x =>
            _tenant.PodeVerTodosOsTenants || x.MerchantId == _tenant.MerchantId);

        // O item também filtra por conta própria. Depender de chegar pelo
        // Pedido deixaria uma consulta direta em pedido_itens sem proteção.
        modelBuilder.Entity<ItemPedido>().HasQueryFilter(x =>
            _tenant.PodeVerTodosOsTenants || x.MerchantId == _tenant.MerchantId);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Dinheiro nunca em ponto flutuante.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}
