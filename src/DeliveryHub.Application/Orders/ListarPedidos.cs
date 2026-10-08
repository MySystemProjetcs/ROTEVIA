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
    IReadOnlyList<ItemDoPedidoDto> Itens,
    Guid? EntregadorId,
    string? EntregadorNome,
    // Só preenchido na listagem do motoboy — ele pode atender mais de um
    // restaurante, e não tem outro jeito de saber de qual loja é o pedido.
    // Sem valor padrão de propósito: as duas projeções são árvore de expressão
    // do EF, e árvore de expressão não aceita argumento omitido (CS0854).
    string? NomeLoja,
    // De onde o pedido veio, pra tela mostrar o selo de origem no card. Só
    // distingue "IFood" de "Interno" por ora — a 99Food está pausada, e sem
    // merchant real cadastrado nenhum pedido dela chega aqui de verdade; o
    // dia que voltar, este campo precisa aprender a terceira origem.
    string Origem,
    // Sem estes dois, a tela do motoboy não tem como saber que precisa pedir
    // o código ao cliente antes de "Finalizar entrega" — e o botão falharia
    // contra Pedido.ConcluirPeloEntregador sem explicação nenhuma na tela.
    bool ExigeCodigoDeEntrega,
    DateTimeOffset? CodigoConfirmadoEm,
    // Pedidos casados: id da corrida e a posição da parada na rota (1-based).
    // Nulos = entrega solo. A tela do motoboy agrupa por lote e ordena pela
    // rota; a do dono mostra o selo do lote.
    Guid? LoteEntregaId,
    int? OrdemNaRota,
    // Código de 4 dígitos que o iFood envia junto do pedido (pickupCode). O
    // motoboy vê no card para confirmar com o cliente. Nulo quando o pedido
    // não exige código.
    string? CodigoDeEntrega);

public static class OrigemDoPedido
{
    public const string Interno = "Interno";
    public const string IFood = "IFood";
}

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
