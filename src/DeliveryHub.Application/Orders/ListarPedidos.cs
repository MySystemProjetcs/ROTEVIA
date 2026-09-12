using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Orders;

namespace DeliveryHub.Application.Orders;

public sealed record ItemDoPedidoDto(
    int Indice,
    string Nome,
    int Quantidade,
    string Unidade,
    decimal PrecoUnitario,
    decimal PrecoTotal,
    string? Observacoes);

public sealed record PedidoDto(
    Guid Id,
    string NumeroExibicao,
    StatusPedido Status,
    bool EhTeste,
    decimal ValorTotal,
    decimal TaxaEntrega,
    string ClienteNome,
    string? EnderecoResumido,
    DateTimeOffset CriadoNaOrigemEm,
    DateTimeOffset RecebidoEm,
    // Instante em que o prazo de confirmação do iFood expira. Vem calculado do
    // servidor de propósito: se o relógio partisse do navegador, um F5 zeraria
    // a contagem e o lojista perderia o pedido achando que ainda tinha tempo.
    DateTimeOffset PrazoConfirmacaoAte,
    IReadOnlyList<ItemDoPedidoDto> Itens);

public interface IListarPedidos
{
    // Sem filtro explícito de merchant: quem isola é o Global Query Filter do
    // DbContext, não este código (CLAUDE.md §6, camada 1).
    Task<IReadOnlyList<PedidoDto>> ExecutarAsync(bool apenasAtivos, CancellationToken ct);
}
