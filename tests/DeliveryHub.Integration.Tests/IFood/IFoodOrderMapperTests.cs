using System.Text.Json;
using DeliveryHub.Domain.Orders;
using DeliveryHub.Infrastructure.Integrations.IFood;
using DeliveryHub.Infrastructure.Integrations.IFood.Contracts;

namespace DeliveryHub.Integration.Tests.IFood;

// Roda sobre o payload real capturado do sandbox, não sobre exemplo de schema.
public sealed class IFoodOrderMapperTests
{
    private static readonly Guid MerchantId = Guid.CreateVersion7();
    private static readonly DateTimeOffset RecebidoEm = new(2026, 9, 11, 23, 10, 0, TimeSpan.Zero);

    private static Pedido Mapear()
    {
        var json = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "ifood", "orders", "order-details-delivery.json"));

        var origem = JsonSerializer.Deserialize<IFoodOrderDetails>(json)!;

        return IFoodOrderMapper.ParaPedido(origem, MerchantId, RecebidoEm);
    }

    [Fact]
    public void Mapeia_identidade_e_tenant()
    {
        var pedido = Mapear();

        Assert.Equal(MerchantId, pedido.MerchantId);
        Assert.Equal("b57177eb-158b-4308-92ca-56aaaecad387", pedido.IdExterno);
        Assert.Equal("1578", pedido.NumeroExibicao);
        Assert.Equal(StatusPedido.Recebido, pedido.Status);
    }

    [Fact]
    public void Preserva_a_marcacao_de_pedido_de_teste()
    {
        Assert.True(Mapear().EhTeste);
    }

    [Fact]
    public void Descarta_o_documento_do_cliente()
    {
        // O payload traz documentNumber e documentType. Entrega não precisa de
        // CPF, então o domínio não tem onde guardar — minimização de dado
        // pessoal é estrutural, não disciplina de quem codifica.
        var cliente = Mapear().Cliente;

        Assert.DoesNotContain(cliente.GetType().GetProperties(), p =>
            p.Name.Contains("Documento", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Cpf", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Mapeia_o_endereco_com_coordenadas()
    {
        var endereco = Mapear().EnderecoEntrega;

        Assert.NotNull(endereco);
        Assert.NotEqual(0, endereco.Latitude);
        Assert.NotEqual(0, endereco.Longitude);
        Assert.False(string.IsNullOrWhiteSpace(endereco.Cidade));
    }

    [Fact]
    public void Mapeia_todos_os_itens_do_pedido()
    {
        var pedido = Mapear();

        Assert.Equal(2, pedido.Itens.Count);
        Assert.All(pedido.Itens, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Nome));
            Assert.True(item.Quantidade > 0);
            Assert.True(item.PrecoTotal > 0);
        });
    }

    [Fact]
    public void Mapeia_os_valores_do_pedido()
    {
        var pedido = Mapear();

        Assert.True(pedido.ValorTotal > 0);
        Assert.True(pedido.TaxaEntrega >= 0);
    }

    [Fact]
    public void Nenhum_tipo_do_iFood_atravessa_para_o_dominio()
    {
        // O isolamento do CLAUDE.md §4: o domínio conhece Pedido, nunca
        // IFoodOrderDetails.
        var tiposExpostos = typeof(Pedido).GetProperties()
            .Select(p => p.PropertyType)
            .Concat(typeof(ItemPedido).GetProperties().Select(p => p.PropertyType));

        Assert.DoesNotContain(tiposExpostos, t =>
            t.Namespace?.Contains("Integrations.IFood", StringComparison.Ordinal) == true);
    }
}
