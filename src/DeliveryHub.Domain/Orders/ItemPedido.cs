using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Domain.Orders;

// Carrega o MerchantId mesmo sendo alcançável pelo Pedido: sem ele, uma
// consulta direta em pedido_itens escaparia do filtro por tenant e devolveria
// item de qualquer loja.
public sealed class ItemPedido : ITenantOwned
{
    private ItemPedido() { }

    public Guid Id { get; private set; }
    public Guid PedidoId { get; private set; }
    public Guid MerchantId { get; private set; }
    public int Indice { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public int Quantidade { get; private set; }
    public string Unidade { get; private set; } = string.Empty;
    public decimal PrecoUnitario { get; private set; }

    // Já inclui opções e customizações do item, como o iFood calcula.
    public decimal PrecoTotal { get; private set; }

    public string? Observacoes { get; private set; }

    public static ItemPedido Criar(Guid pedidoId, Guid merchantId, int indice, string nome, int quantidade,
        string unidade, decimal precoUnitario, decimal precoTotal, string? observacoes) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            PedidoId = pedidoId,
            MerchantId = merchantId,
            Indice = indice,
            Nome = nome,
            Quantidade = quantidade,
            Unidade = unidade,
            PrecoUnitario = precoUnitario,
            PrecoTotal = precoTotal,
            Observacoes = observacoes
        };
}
