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
    // Coordenada do destino, para o motoboy abrir a navegação no ponto exato.
    // Nula quando desconhecida — inclusive nos pedidos do sandbox do iFood, que
    // chegam com 0,0 e levariam a navegação para o meio do Atlântico.
    double? EnderecoLatitude,
    double? EnderecoLongitude,
    // Como foi pago, e quanto ainda falta receber. Valor a cobrar maior que
    // zero é o que liga a etiqueta "Cobrar" e o passo extra do motoboy.
    string PagamentoDescricao,
    decimal PagamentoValorACobrar,
    DateTimeOffset CriadoNaOrigemEm,
    DateTimeOffset RecebidoEm,
    // Instante em que o prazo de confirmação do iFood expira. Vem calculado do
    // servidor de propósito: se o relógio partisse do navegador, um F5 zeraria
    // a contagem e o lojista perderia o pedido achando que ainda tinha tempo.
    DateTimeOffset PrazoConfirmacaoAte,
    IReadOnlyList<ItemDoPedidoDto> Itens,
    Guid? EntregadorId,
    string? EntregadorNome,
    // Só preenchido na listagem do motoboy — ele pode atender mais de um
    // restaurante, e não tem outro jeito de saber de qual loja é o pedido.
    // Sem valor padrão de propósito: as duas projeções são árvore de expressão
    // do EF, e árvore de expressão não aceita argumento omitido (CS0854).
    string? NomeLoja);

public interface IListarPedidos
{
    // Sem filtro explícito de merchant: quem isola é o Global Query Filter do
    // DbContext, não este código (CLAUDE.md §6, camada 1).
    Task<IReadOnlyList<PedidoDto>> ExecutarAsync(bool apenasAtivos, CancellationToken ct);
}

public interface IListarMinhasEntregas
{
    // Aqui sim precisa de filtro explícito: o motoboy não tem merchant_id no
    // token pro Global Query Filter isolar sozinho, e ele pode legitimamente
    // enxergar pedidos de mais de um restaurante.
    Task<IReadOnlyList<PedidoDto>> ExecutarAsync(Guid usuarioLogadoId, CancellationToken ct);
}
