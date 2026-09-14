using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Orders;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Application.Orders;

public sealed record ItemDoLancamento(
    string Nome,
    int Quantidade,
    decimal PrecoUnitario,
    string? Observacoes);

public sealed record NovoPedidoInterno(
    string ClienteNome,
    string? ClienteTelefone,
    string Cep,
    string Numero,
    string? Complemento,
    string? Referencia,
    // Como o cliente paga, e se já pagou. Venda de balcão não tem marketplace
    // informando isso — quem sabe é quem atendeu.
    string FormaPagamento,
    bool JaPago,
    IReadOnlyList<ItemDoLancamento> Itens,
    // Opcional: em venda de balcão o lojista costuma já saber quem entrega, e
    // obrigá-lo a passar por Confirmar/Preparo/Pronto antes de alocar seria
    // burocracia de um fluxo que existe para o marketplace, não para ele.
    Guid? EntregadorId);

public static class PedidoInternoErrors
{
    public static readonly Error SemItens = new(
        "pedido_interno.sem_itens",
        "Informe ao menos um item.",
        ErrorType.Validation);

    public static readonly Error ItemInvalido = new(
        "pedido_interno.item_invalido",
        "Item precisa de nome, quantidade maior que zero e preço não negativo.",
        ErrorType.Validation);

    public static readonly Error CepNaoEncontrado = new(
        "pedido_interno.cep_nao_encontrado",
        "CEP não encontrado.",
        ErrorType.Validation);

    public static readonly Error EntregadorInvalido = new(
        "pedido_interno.entregador_invalido",
        "Entregador sem vínculo ativo com esta loja.",
        ErrorType.Validation);

    public static readonly Error SemLoja = new(
        "pedido_interno.sem_loja",
        "Sessão sem loja.",
        ErrorType.Validation);
}

public interface ILancarPedidoInterno
{
    Task<Result<Guid>> ExecutarAsync(NovoPedidoInterno novo, CancellationToken ct);
}

// Venda que nasce dentro do sistema — balcão, telefone, WhatsApp. O §5 já
// previa "PDV próprio" como origem de pedido ao lado do iFood.
public sealed class LancarPedidoInterno : ILancarPedidoInterno
{
    private readonly IPedidoRepository _pedidos;
    private readonly ICourierRepository _couriers;
    private readonly IResolverEndereco _enderecos;
    private readonly ITenantContext _tenant;
    private readonly TimeProvider _relogio;

    public LancarPedidoInterno(
        IPedidoRepository pedidos,
        ICourierRepository couriers,
        IResolverEndereco enderecos,
        ITenantContext tenant,
        TimeProvider relogio)
    {
        _pedidos = pedidos;
        _couriers = couriers;
        _enderecos = enderecos;
        _tenant = tenant;
        _relogio = relogio;
    }

    // Pago = nada pendente; a cobrar = o total inteiro na mão do motoboy.
    // Venda interna não tem pagamento parcial: quem atendeu sabe se recebeu ou
    // não, e dividir isso em dois valores seria complexidade sem uso hoje.
    private static Pagamento MontarPagamento(NovoPedidoInterno novo, decimal total) =>
        novo.JaPago
            ? new Pagamento(total, 0, novo.FormaPagamento)
            : new Pagamento(0, total, novo.FormaPagamento);

    public async Task<Result<Guid>> ExecutarAsync(NovoPedidoInterno novo, CancellationToken ct)
    {
        if (_tenant.MerchantId is not { } merchantId)
            return Result.Failure<Guid>(PedidoInternoErrors.SemLoja);

        if (novo.Itens.Count == 0)
            return Result.Failure<Guid>(PedidoInternoErrors.SemItens);

        if (novo.Itens.Any(i => string.IsNullOrWhiteSpace(i.Nome) || i.Quantidade <= 0 || i.PrecoUnitario < 0))
            return Result.Failure<Guid>(PedidoInternoErrors.ItemInvalido);

        var endereco = await _enderecos.PorCepAsync(novo.Cep, novo.Numero, ct);
        if (endereco is null)
            return Result.Failure<Guid>(PedidoInternoErrors.CepNaoEncontrado);

        var agora = _relogio.GetUtcNow();
        var total = novo.Itens.Sum(i => i.PrecoUnitario * i.Quantidade);

        var pedido = Pedido.Receber(
            merchantId: merchantId,
            // Prefixo local: sem marketplace para avisar, o AvancarPedido pula a
            // chamada externa (senão toda transição devolveria recusa).
            idExterno: $"{Pedido.PrefixoOrigemLocal}{Guid.CreateVersion7()}",
            numeroExibicao: Random.Shared.Next(1000, 9999).ToString(),
            // Venda de verdade: conta na receita e gera repasse ao entregador.
            ehTeste: false,
            cliente: new Cliente(novo.ClienteNome, novo.ClienteTelefone, null),
            enderecoEntrega: new Endereco(
                Logradouro: endereco.Logradouro,
                Numero: novo.Numero,
                Bairro: endereco.Bairro,
                Cidade: endereco.Cidade,
                Estado: endereco.Estado,
                Cep: endereco.Cep,
                Complemento: novo.Complemento,
                Referencia: novo.Referencia,
                // Sem coordenada o pedido existe, só não vai para o mapa.
                Latitude: endereco.Latitude ?? 0,
                Longitude: endereco.Longitude ?? 0),
            valorTotal: total,
            // O que o motoboy recebe por esta entrega não sai daqui: é a taxa
            // padrão da loja, lida no fechamento (AvancarEntrega). Guardar um
            // valor por pedido aqui criaria uma segunda fonte da mesma regra.
            taxaEntrega: 0m,
            criadoNaOrigemEm: agora,
            recebidoEm: agora,
            pagamento: MontarPagamento(novo, total));

        var indice = 1;
        foreach (var item in novo.Itens)
        {
            pedido.AdicionarItem(
                indice++,
                item.Nome,
                item.Quantidade,
                "UN",
                item.PrecoUnitario,
                item.PrecoUnitario * item.Quantidade,
                item.Observacoes);
        }

        // Motoboy já escolhido no lançamento: só define quem entrega, sem mover
        // o status — o pedido ainda passa por Confirmar/Preparo/Pronto antes do
        // Despachar, igual a qualquer outro.
        if (novo.EntregadorId is { } entregadorId)
        {
            if (!await _couriers.ExisteVinculoAtivoAsync(entregadorId, merchantId, ct))
                return Result.Failure<Guid>(PedidoInternoErrors.EntregadorInvalido);

            pedido.AlocarEntregador(entregadorId);
        }

        _pedidos.Adicionar(pedido);
        await _pedidos.SalvarAsync(ct);

        return Result.Success(pedido.Id);
    }
}
