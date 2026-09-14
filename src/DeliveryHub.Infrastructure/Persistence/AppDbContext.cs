using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Couriers;
using DeliveryHub.Domain.Identity;
using DeliveryHub.Domain.Merchants;
using DeliveryHub.Domain.Orders;
using DeliveryHub.Domain.Tracking;
using DeliveryHub.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace DeliveryHub.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    private readonly ITenantContext _tenant;
    private readonly ICredentialCipher _cipher;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant, ICredentialCipher cipher)
        : base(options)
    {
        _tenant = tenant;
        _cipher = cipher;
    }

    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<UsuarioMerchant> UsuarioMerchants => Set<UsuarioMerchant>();
    public DbSet<IntegrationInboxEvent> IntegrationInbox => Set<IntegrationInboxEvent>();
    public DbSet<Courier> Couriers => Set<Courier>();
    public DbSet<CourierMerchantLink> CourierMerchantLinks => Set<CourierMerchantLink>();
    public DbSet<PosicaoEntregador> PosicoesEntregador => Set<PosicaoEntregador>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // MerchantConfiguration recebe o cifrador por construtor — não pode
        // vir do scan por assembly, que só sabe instanciar sem parâmetro.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly,
            tipo => tipo != typeof(MerchantConfiguration));

        new MerchantConfiguration(_cipher).Configure(modelBuilder.Entity<Merchant>());

        // Camada 1 do isolamento (CLAUDE.md §6): o filtro é estrutural, não
        // depende de ninguém lembrar de escrever Where(x => x.MerchantId == ...).
        // Requisição sem tenant e sem permissão ampla não enxerga nada.
        modelBuilder.Entity<Pedido>().HasQueryFilter(x =>
            _tenant.PodeVerTodosOsTenants || x.MerchantId == _tenant.MerchantId);

        // O item também filtra por conta própria. Depender de chegar pelo
        // Pedido deixaria uma consulta direta em pedido_itens sem proteção.
        modelBuilder.Entity<ItemPedido>().HasQueryFilter(x =>
            _tenant.PodeVerTodosOsTenants || x.MerchantId == _tenant.MerchantId);

        // Courier é global de propósito (CLAUDE.md §6) — só o vínculo com a
        // loja é tenant-owned.
        modelBuilder.Entity<CourierMerchantLink>().HasQueryFilter(x =>
            _tenant.PodeVerTodosOsTenants || x.MerchantId == _tenant.MerchantId);

        // O rastreio também filtra: uma loja não acompanha o motoboy que está
        // entregando para outra.
        modelBuilder.Entity<PosicaoEntregador>().HasQueryFilter(x =>
            _tenant.PodeVerTodosOsTenants || x.MerchantId == _tenant.MerchantId);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Dinheiro nunca em ponto flutuante.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}
