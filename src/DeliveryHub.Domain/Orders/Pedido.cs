using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Domain.Orders;

public sealed class Pedido : ITenantOwned
{
    private readonly List<ItemPedido> _itens = [];

    // Usado só pelo EF Core: tipos owned (Cliente, Endereco) não podem ser
    // injetados por construtor, então a materialização passa por aqui.
    private Pedido() { }

    private Pedido(
        Guid id,
        Guid merchantId,
        string idExterno,
        string numeroExibicao,
        bool ehTeste,
        Cliente cliente,
        Endereco? enderecoEntrega,
        decimal valorTotal,
        decimal taxaEntrega,
        DateTimeOffset criadoNaOrigemEm,
        DateTimeOffset recebidoEm)
    {
        Id = id;
        MerchantId = merchantId;
        IdExterno = idExterno;
        NumeroExibicao = numeroExibicao;
        EhTeste = ehTeste;
        Cliente = cliente;
        EnderecoEntrega = enderecoEntrega;
        ValorTotal = valorTotal;
        TaxaEntrega = taxaEntrega;
        CriadoNaOrigemEm = criadoNaOrigemEm;
        RecebidoEm = recebidoEm;
        Status = StatusPedido.Recebido;
    }

    public Guid Id { get; }
    public Guid MerchantId { get; }

    // Id do pedido na origem. É por ele que a ingestão deduplica.
    public string IdExterno { get; }

    // O número curto que o lojista vê e usa para falar do pedido.
    public string NumeroExibicao { get; }

    // Vem do isTest do payload do iFood. Precisa sobreviver até o Ledger:
    // pedido de sandbox não pode virar lançamento financeiro.
    public bool EhTeste { get; }

    public Cliente Cliente { get; private set; } = null!;

    // Nulo quando o pedido é retirada no balcão ou consumo no local.
    public Endereco? EnderecoEntrega { get; private set; }

    public decimal ValorTotal { get; }
    public decimal TaxaEntrega { get; }
    public DateTimeOffset CriadoNaOrigemEm { get; }
    public DateTimeOffset RecebidoEm { get; }
    public StatusPedido Status { get; private set; }

    public IReadOnlyList<ItemPedido> Itens => _itens;

    public static Pedido Receber(
        Guid merchantId,
        string idExterno,
        string numeroExibicao,
        bool ehTeste,
        Cliente cliente,
        Endereco? enderecoEntrega,
        decimal valorTotal,
        decimal taxaEntrega,
        DateTimeOffset criadoNaOrigemEm,
        DateTimeOffset recebidoEm) =>
        new(Guid.CreateVersion7(), merchantId, idExterno, numeroExibicao, ehTeste, cliente,
            enderecoEntrega, valorTotal, taxaEntrega, criadoNaOrigemEm, recebidoEm);

    // O item herda o tenant do pedido: é o pedido que sabe de quem ele é, e
    // deixar isso a cargo de quem chama seria abrir espaço para item órfão.
    public void AdicionarItem(int indice, string nome, int quantidade, string unidade,
        decimal precoUnitario, decimal precoTotal, string? observacoes) =>
        _itens.Add(ItemPedido.Criar(Id, MerchantId, indice, nome, quantidade, unidade, precoUnitario, precoTotal, observacoes));

    public Result Confirmar() => AvancarPara(StatusPedido.Confirmado);
    public Result IniciarPreparo() => AvancarPara(StatusPedido.EmPreparo);
    public Result MarcarPronto() => AvancarPara(StatusPedido.Pronto);
    public Result Despachar() => AvancarPara(StatusPedido.Despachado);
    public Result Concluir() => AvancarPara(StatusPedido.Concluido);

    public Result Cancelar()
    {
        if (Status == StatusPedido.Cancelado)
            return Result.Success();

        if (Status == StatusPedido.Concluido)
            return Result.Failure(PedidoErrors.PedidoConcluido);

        Status = StatusPedido.Cancelado;
        return Result.Success();
    }

    private Result AvancarPara(StatusPedido destino)
    {
        if (Status == StatusPedido.Cancelado)
            return Result.Failure(PedidoErrors.PedidoCancelado);

        if (Status == StatusPedido.Concluido)
            return destino == StatusPedido.Concluido
                ? Result.Success()
                : Result.Failure(PedidoErrors.PedidoConcluido);

        // Progressão monotônica: evento atrasado ou repetido é no-op de sucesso,
        // nunca erro e nunca retrocesso. O polling não garante ordem, então um
        // DSP chegando depois de CON não pode desfazer a conclusão.
        if (destino <= Status)
            return Result.Success();

        Status = destino;
        return Result.Success();
    }
}
