using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Merchants;
using DeliveryHub.Domain.Orders;
using DeliveryHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DeliveryHub.Integration.Tests.Persistence;

// O filtro por tenant é a camada 1 do CLAUDE.md §6. Testar contra o Postgres
// real importa porque o filtro vira SQL: um erro de tradução só aparece aqui.
[Collection("banco-local")]
public sealed class IsolamentoMultiTenantTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=deliveryhub;Username=deliveryhub;Password=deliveryhub_dev";

    private sealed class TenantFixo : ITenantContext
    {
        public TenantFixo(Guid? merchantId, bool amplo = false)
        {
            MerchantId = merchantId;
            PodeVerTodosOsTenants = amplo;
        }

        public bool EstaAutenticado => true;
        public Guid? UsuarioId => Guid.Empty;
        public Guid? MerchantId { get; }
        public bool PodeVerTodosOsTenants { get; }
    }

    private Guid _lojaA;
    private Guid _lojaB;
    private readonly List<string> _idsExternos = [];

    private static AppDbContext Contexto(ITenantContext tenant) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options, tenant, new CifradorDeTeste());

    public async Task InitializeAsync()
    {
        await using var db = Contexto(new TenantContextSistema());

        var a = Merchant.Criar("Loja A", DateTimeOffset.UtcNow);
        var b = Merchant.Criar("Loja B", DateTimeOffset.UtcNow);
        db.Merchants.AddRange(a, b);
        await db.SaveChangesAsync();

        _lojaA = a.Id;
        _lojaB = b.Id;

        db.Pedidos.Add(PedidoDe(_lojaA, "iso-a-1"));
        db.Pedidos.Add(PedidoDe(_lojaA, "iso-a-2"));
        db.Pedidos.Add(PedidoDe(_lojaB, "iso-b-1"));
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = Contexto(new TenantContextSistema());
        foreach (var id in _idsExternos)
            await db.Database.ExecuteSqlAsync($"DELETE FROM pedidos WHERE id_externo = {id}");

        await db.Database.ExecuteSqlAsync($"DELETE FROM merchants WHERE id IN ({_lojaA}, {_lojaB})");
    }

    private Pedido PedidoDe(Guid merchantId, string idExterno)
    {
        _idsExternos.Add(idExterno);

        return Pedido.Receber(
            merchantId: merchantId,
            idExterno: idExterno,
            numeroExibicao: "0001",
            ehTeste: true,
            cliente: new Cliente("Cliente", null, null),
            enderecoEntrega: null,
            valorTotal: 10m,
            taxaEntrega: 0m,
            criadoNaOrigemEm: DateTimeOffset.UtcNow,
            recebidoEm: DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Loja_enxerga_apenas_os_proprios_pedidos()
    {
        await using var db = Contexto(new TenantFixo(_lojaA));

        var pedidos = await db.Pedidos.AsNoTracking().Where(x => _idsExternos.Contains(x.IdExterno)).ToListAsync();

        Assert.Equal(2, pedidos.Count);
        Assert.All(pedidos, p => Assert.Equal(_lojaA, p.MerchantId));
    }

    [Fact]
    public async Task Pedido_de_outra_loja_nao_e_encontrado_nem_por_id()
    {
        // Buscar direto pelo id tem que devolver nada, não um erro de
        // autorização: 404 não revela que o pedido existe.
        await using var db = Contexto(new TenantFixo(_lojaA));

        var pedido = await db.Pedidos.AsNoTracking().FirstOrDefaultAsync(x => x.IdExterno == "iso-b-1");

        Assert.Null(pedido);
    }

    [Fact]
    public async Task Requisicao_sem_tenant_nao_enxerga_nada()
    {
        // Falha fechado: contexto sem loja e sem permissão ampla não vê pedido
        // de ninguém, em vez de ver todos.
        await using var db = Contexto(new TenantFixo(merchantId: null));

        var pedidos = await db.Pedidos.AsNoTracking().Where(x => _idsExternos.Contains(x.IdExterno)).ToListAsync();

        Assert.Empty(pedidos);
    }

    [Fact]
    public async Task Administrador_enxerga_todas_as_lojas()
    {
        await using var db = Contexto(new TenantFixo(merchantId: null, amplo: true));

        var pedidos = await db.Pedidos.AsNoTracking().Where(x => _idsExternos.Contains(x.IdExterno)).ToListAsync();

        Assert.Equal(3, pedidos.Count);
    }
}
