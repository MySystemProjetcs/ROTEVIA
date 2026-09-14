using System.Text.Json;
using DeliveryHub.Domain.Merchants;
using DeliveryHub.Domain.Orders;
using DeliveryHub.Infrastructure.Integrations.IFood;
using DeliveryHub.Infrastructure.Integrations.IFood.Contracts;
using DeliveryHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DeliveryHub.Integration.Tests.Persistence;

// Usa o Postgres do docker-compose. Não é a solução final — o ENGINEERING-GUIDE
// §9 pede Testcontainers —, mas prova a materialização contra um banco de
// verdade em vez de um provider em memória, que esconde erro de mapeamento.
[Collection("banco-local")]
public sealed class PedidoPersistenciaTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=deliveryhub;Username=deliveryhub;Password=deliveryhub_dev";

    private AppDbContext _db = null!;
    private Guid _merchantId;

    // O fixture usa um id de pedido real, e o worker pode ter gravado esse mesmo
    // pedido no banco de desenvolvimento. Como o índice ux_pedidos_id_externo é
    // global, o teste precisa se isolar por esse id — não só pelo merchant.
    private const string IdExternoDoFixture = "b57177eb-158b-4308-92ca-56aaaecad387";

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options;
        _db = new AppDbContext(options, new TenantContextSistema(), new CifradorDeTeste());

        await LimparAsync();

        var merchant = Merchant.Criar("Loja de Teste", DateTimeOffset.UtcNow);
        _db.Merchants.Add(merchant);
        await _db.SaveChangesAsync();
        _merchantId = merchant.Id;
    }

    public async Task DisposeAsync()
    {
        await LimparAsync();
        await _db.Database.ExecuteSqlAsync($"DELETE FROM merchants WHERE id = {_merchantId}");
        await _db.DisposeAsync();
    }

    private async Task LimparAsync()
    {
        await _db.Database.ExecuteSqlAsync($"DELETE FROM pedidos WHERE id_externo = {IdExternoDoFixture}");

        if (_merchantId != Guid.Empty)
            await _db.Database.ExecuteSqlAsync($"DELETE FROM pedidos WHERE merchant_id = {_merchantId}");
    }

    [Fact]
    public async Task Pedido_real_sobrevive_ao_ciclo_de_gravar_e_reler()
    {
        var json = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "ifood", "orders", "order-details-delivery.json"));
        var origem = JsonSerializer.Deserialize<IFoodOrderDetails>(json)!;

        var pedido = IFoodOrderMapper.ParaPedido(origem, _merchantId, DateTimeOffset.UtcNow);
        pedido.Confirmar();

        _db.Pedidos.Add(pedido);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var lido = await _db.Pedidos
            .AsNoTracking()
            .Include(x => x.Itens)
            .SingleAsync(x => x.Id == pedido.Id);

        Assert.Equal("1578", lido.NumeroExibicao);
        Assert.Equal(StatusPedido.Confirmado, lido.Status);
        Assert.True(lido.EhTeste);
        Assert.Equal(2, lido.Itens.Count);

        // Tipos owned: é aqui que mapeamento errado aparece como null.
        Assert.NotNull(lido.Cliente);
        Assert.False(string.IsNullOrWhiteSpace(lido.Cliente.Nome));
        Assert.NotNull(lido.EnderecoEntrega);
        Assert.NotEqual(0, lido.EnderecoEntrega!.Latitude);
    }

    [Fact]
    public async Task Pedido_duplicado_esbarra_na_constraint_do_banco()
    {
        var json = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "ifood", "orders", "order-details-delivery.json"));
        var origem = JsonSerializer.Deserialize<IFoodOrderDetails>(json)!;

        _db.Pedidos.Add(IFoodOrderMapper.ParaPedido(origem, _merchantId, DateTimeOffset.UtcNow));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        // Mesmo id externo: quem barra é ux_pedidos_id_externo, não um if.
        _db.Pedidos.Add(IFoodOrderMapper.ParaPedido(origem, _merchantId, DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }
}

[CollectionDefinition("banco-local", DisableParallelization = true)]
public sealed class BancoLocalCollection;
