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

    // Regressão do achado da auditoria: "Para pedidos em que a entrega é pela
    // plataforma 99, o estabelecimento irá receber o dinheiro diretamente como
    // método 'online', independente se o pagamento for em dinheiro... —
    // considerar o pedido como já pago e sem valores pendentes" (guia da
    // 99Food, "Obs: Pagamentos"). Antes da correção, pay_type=2 (dinheiro)
    // virava "a cobrar" mesmo com delivery_type=1 (entrega pela 99) — um
    // motoboy que não existe (é da 99, não da loja) nunca cobraria ninguém.
    [Fact]
    public void ParaPedido_com_entrega_pela_99_e_pagamento_em_dinheiro_e_tratado_como_ja_pago()
    {
        var model = new DiDiOrderModel
        {
            OrderId = 111,
            OrderIndex = 1,
            PayType = 2, // dinheiro
            DeliveryType = 1, // entrega pela DiDi/99Food
            Price = new DiDiPriceModel { OrderPrice = 4000, RealPayPrice = 4000 },
        };

        var pedido = DiDiFoodOrderMapper.ParaPedido(model, Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.True(pedido.EntregaPeloParceiro);
        Assert.False(pedido.Pagamento.PrecisaCobrarNaEntrega);
        Assert.Equal(40.00m, pedido.Pagamento.ValorJaPago);
        Assert.Equal(0m, pedido.Pagamento.ValorACobrar);
    }

    // O espelho: mesma forma de pagamento, mas entrega pela loja (delivery_type
    // = 2) — aí sim o motoboy da loja precisa cobrar, então o pay_type continua
    // valendo como antes.
    [Fact]
    public void ParaPedido_com_entrega_pela_loja_e_pagamento_em_dinheiro_continua_a_cobrar()
    {
        var model = new DiDiOrderModel
        {
            OrderId = 112,
            OrderIndex = 2,
            PayType = 2, // dinheiro
            DeliveryType = 2, // entrega pela loja
            Price = new DiDiPriceModel { OrderPrice = 4000, RealPayPrice = 4000 },
        };

        var pedido = DiDiFoodOrderMapper.ParaPedido(model, Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.False(pedido.EntregaPeloParceiro);
        Assert.True(pedido.Pagamento.PrecisaCobrarNaEntrega);
        Assert.Equal(0m, pedido.Pagamento.ValorJaPago);
        Assert.Equal(40.00m, pedido.Pagamento.ValorACobrar);
    }
}
