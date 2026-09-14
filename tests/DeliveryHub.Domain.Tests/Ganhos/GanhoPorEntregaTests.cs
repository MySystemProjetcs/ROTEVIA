using DeliveryHub.Domain.Couriers;
using DeliveryHub.Domain.Merchants;
using DeliveryHub.Domain.Orders;

namespace DeliveryHub.Domain.Tests.Ganhos;

public sealed class GanhoPorEntregaTests
{
    private static Pedido NovoPedido(bool ehTeste = false) => Pedido.Receber(
        merchantId: Guid.CreateVersion7(),
        idExterno: Guid.CreateVersion7().ToString(),
        numeroExibicao: "1578",
        ehTeste: ehTeste,
        cliente: new Cliente("Cliente de Teste", null, null),
        enderecoEntrega: null,
        valorTotal: 27m,
        taxaEntrega: 0m,
        criadoNaOrigemEm: DateTimeOffset.UtcNow,
        recebidoEm: DateTimeOffset.UtcNow);

    [Fact]
    public void Taxa_negativa_e_recusada()
    {
        var merchant = Merchant.Criar("Loja", DateTimeOffset.UtcNow);

        var resultado = merchant.DefinirTaxaPorEntrega(-1m);

        Assert.True(resultado.IsFailure);
        Assert.Equal(MerchantErrors.TaxaInvalida, resultado.Error);
        Assert.Equal(0m, merchant.TaxaPadraoPorEntrega);
    }

    [Fact]
    public void Taxa_zero_ou_positiva_e_aceita()
    {
        var merchant = Merchant.Criar("Loja", DateTimeOffset.UtcNow);

        Assert.True(merchant.DefinirTaxaPorEntrega(7.5m).IsSuccess);
        Assert.Equal(7.5m, merchant.TaxaPadraoPorEntrega);
    }

    [Fact]
    public void Repasse_grava_uma_vez_e_nao_muda_mais()
    {
        var pedido = NovoPedido();

        Assert.True(pedido.RegistrarRepasseAoEntregador(7.5m).IsSuccess);
        Assert.Equal(7.5m, pedido.ValorPagoAoEntregador);

        // Reentrega (webhook duplicado, retry): no-op de sucesso, valor intacto.
        Assert.True(pedido.RegistrarRepasseAoEntregador(9m).IsSuccess);
        Assert.Equal(7.5m, pedido.ValorPagoAoEntregador);
    }

    [Fact]
    public void Repasse_negativo_e_recusado()
    {
        var pedido = NovoPedido();

        var resultado = pedido.RegistrarRepasseAoEntregador(-1m);

        Assert.True(resultado.IsFailure);
        Assert.Equal(PedidoErrors.ValorRepasseInvalido, resultado.Error);
        Assert.Null(pedido.ValorPagoAoEntregador);
    }

    [Fact]
    public void Motoboy_nasce_disponivel_e_pode_desligar_e_religar()
    {
        var courier = Courier.Criar("12345678901", "Maria", "5511999998888", "Honda Biz", "ABC1D23", DateTimeOffset.UtcNow);

        Assert.True(courier.DisponivelParaEntrega);

        courier.DefinirDisponibilidade(false);
        Assert.False(courier.DisponivelParaEntrega);

        courier.DefinirDisponibilidade(true);
        Assert.True(courier.DisponivelParaEntrega);
    }
}
