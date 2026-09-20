using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Domain.Orders;

public static class PedidoErrors
{
    public static readonly Error PedidoConcluido = new(
        "pedido.concluido",
        "Pedido já concluído não aceita mudança de status.",
        ErrorType.Conflict);

    public static readonly Error PedidoCancelado = new(
        "pedido.cancelado",
        "Pedido cancelado não aceita mudança de status.",
        ErrorType.Conflict);

    public static readonly Error SemEntregadorAlocado = new(
        "pedido.sem_entregador_alocado",
        "Aloque um motoboy antes de despachar.",
        ErrorType.Conflict);

    public static readonly Error PedidoJaPago = new(
        "pedido.ja_pago",
        "Este pedido já foi pago — não há valor a cobrar na entrega.",
        ErrorType.Conflict);

    public static readonly Error EntregadorNaoPertenceAoPedido = new(
        "pedido.entregador_nao_pertence_ao_pedido",
        "Este pedido não está atribuído a você.",
        ErrorType.NotFound);

    public static readonly Error CodigoDeEntregaPendente = new(
        "pedido.codigo_entrega_pendente",
        "Confirme o código de entrega com o cliente antes de finalizar.",
        ErrorType.Conflict);

    public static readonly Error CodigoDeEntregaInvalido = new(
        "pedido.codigo_entrega_invalido",
        "Código incorreto. Confira com o cliente e tente de novo.",
        ErrorType.Validation);

    public static readonly Error ValorRepasseInvalido = new(
        "pedido.valor_repasse_invalido",
        "O valor pago ao entregador não pode ser negativo.",
        ErrorType.Validation);
}
