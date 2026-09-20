using System.Text.Json;
using DeliveryHub.Domain.Orders;
using DeliveryHub.Infrastructure.Integrations.DiDiFood;
using DeliveryHub.Infrastructure.Integrations.DiDiFood.Contracts;

namespace DeliveryHub.Integration.Tests.Integracoes;

public sealed class DiDiFoodOrderMapperTests
{
    [Fact]
    public void ParaPedido_deve_mapear_fixture_da_didi_corretamente()
    {
        // Arrange
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "didi", "webhook-new-order.json");
        if (!File.Exists(fixturePath))
        {
            fixturePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "fixtures", "didi", "webhook-new-order.json");
        }

        var json = File.ReadAllText(fixturePath);
        var evento = JsonSerializer.Deserialize<DiDiWebhookEvent>(json);
        Assert.NotNull(evento?.Order);

        var merchantId = Guid.NewGuid();
        var recebidoEm = DateTimeOffset.UtcNow;

        // Act
        var pedido = DiDiFoodOrderMapper.ParaPedido(evento.Order, merchantId, recebidoEm);

        // Assert
        Assert.NotNull(pedido);
        Assert.Equal(merchantId, pedido.MerchantId);
        Assert.Equal("2352921557674426622", pedido.IdExterno);
        Assert.Equal("42", pedido.NumeroExibicao);
        Assert.False(pedido.EhTeste);

        // Valores monetários (centavos para decimal)
        Assert.Equal(30.00m, pedido.ValorTotal); // real_pay_price 3000 centavos
        Assert.Equal(5.00m, pedido.TaxaEntrega); // delivery_price 500 centavos

        // Cliente
        Assert.Equal("João Silva", pedido.Cliente.Nome);
        Assert.Equal("11999990000", pedido.Cliente.Telefone);

        // Endereço
        Assert.NotNull(pedido.EnderecoEntrega);
        Assert.Equal("Av. Paulista, 1000", pedido.EnderecoEntrega.Logradouro);
        Assert.Equal("1000", pedido.EnderecoEntrega.Numero);
        Assert.Equal("São Paulo", pedido.EnderecoEntrega.Cidade);
        Assert.Equal(-23.5614, pedido.EnderecoEntrega.Latitude);
        Assert.Equal(-46.6560, pedido.EnderecoEntrega.Longitude);

        // Itens
        Assert.Equal(2, pedido.Itens.Count);

        var item1 = pedido.Itens[0];
        Assert.Equal("X-Burguer", item1.Nome);
        Assert.Equal(1, item1.Quantidade);
        Assert.Equal(25.00m, item1.PrecoUnitario);
        Assert.Equal(25.00m, item1.PrecoTotal);

        var item2 = pedido.Itens[1];
        Assert.Equal("Refrigerante 350ml", item2.Nome);
        Assert.Equal(1, item2.Quantidade);
        Assert.Equal(5.00m, item2.PrecoUnitario);
        Assert.Equal(5.00m, item2.PrecoTotal);

        // Pagamento
        Assert.False(pedido.Pagamento.PrecisaCobrarNaEntrega);
        Assert.Equal(30.00m, pedido.Pagamento.ValorJaPago);
        Assert.Equal(0m, pedido.Pagamento.ValorACobrar);
    }
}
