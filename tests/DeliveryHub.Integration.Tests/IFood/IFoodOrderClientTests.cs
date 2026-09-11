using System.Net;
using DeliveryHub.Infrastructure.Integrations.IFood.Orders;

namespace DeliveryHub.Integration.Tests.IFood;

// Roda sobre o payload real capturado do sandbox em 2026-09-11 (pedido PLACED,
// DELIVERY/IMMEDIATE), com os dados pessoais anonimizados.
public sealed class IFoodOrderClientTests
{
    private static readonly Guid OrderId = Guid.Parse("b57177eb-158b-4308-92ca-56aaaecad387");

    private static string Fixture() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "ifood", "orders", "order-details-delivery.json"));

    private static IFoodOrderClient Build(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://merchant-api.ifood.com.br/order/v1.0/")
        });

    [Fact]
    public async Task Desserializa_o_pedido_real_capturado_do_sandbox()
    {
        var handler = new StubHttpMessageHandler().Enqueue(HttpStatusCode.OK, Fixture());

        var pedido = await Build(handler).GetDetailsAsync(OrderId, CancellationToken.None);

        Assert.Equal("https://merchant-api.ifood.com.br/order/v1.0/orders/b57177eb-158b-4308-92ca-56aaaecad387",
            handler.LastRequestUri?.ToString());

        Assert.Equal(OrderId, pedido.Id);
        Assert.Equal("DELIVERY", pedido.OrderType);
        Assert.Equal("IMMEDIATE", pedido.OrderTiming);
        Assert.Equal("FOOD", pedido.Category);
        Assert.Equal(2, pedido.Items.Count);
        Assert.NotNull(pedido.Delivery);
    }

    [Fact]
    public async Task Le_o_isTest_que_o_schema_publicado_nao_documenta()
    {
        // Pedido de sandbox precisa ser distinguível: se entrar no ledger como
        // pedido real, vira dinheiro inventado no extrato do lojista.
        var pedido = await Build(new StubHttpMessageHandler().Enqueue(HttpStatusCode.OK, Fixture()))
            .GetDetailsAsync(OrderId, CancellationToken.None);

        Assert.True(pedido.IsTest);
    }

    [Fact]
    public async Task Le_o_merchant_que_resolve_o_tenant()
    {
        var pedido = await Build(new StubHttpMessageHandler().Enqueue(HttpStatusCode.OK, Fixture()))
            .GetDetailsAsync(OrderId, CancellationToken.None);

        Assert.Equal(Guid.Parse("323f53b3-356c-4af8-85b7-b71eff95dd72"), pedido.Merchant.Id);
    }

    [Fact]
    public async Task Le_os_valores_monetarios_como_decimal()
    {
        // O iFood manda inteiro quando o valor é zero (ex.: "benefits": 0) e
        // decimal no resto; double introduziria erro de arredondamento em dinheiro.
        var pedido = await Build(new StubHttpMessageHandler().Enqueue(HttpStatusCode.OK, Fixture()))
            .GetDetailsAsync(OrderId, CancellationToken.None);

        Assert.Equal(
            pedido.Total.SubTotal + pedido.Total.DeliveryFee + pedido.Total.AdditionalFees - pedido.Total.Benefits,
            pedido.Total.OrderAmount);
    }

    [Fact]
    public async Task Le_o_item_com_uniqueId_usado_pelo_ORDER_PATCHED()
    {
        var pedido = await Build(new StubHttpMessageHandler().Enqueue(HttpStatusCode.OK, Fixture()))
            .GetDetailsAsync(OrderId, CancellationToken.None);

        Assert.NotNull(pedido.Items[0].UniqueId);
    }
}
