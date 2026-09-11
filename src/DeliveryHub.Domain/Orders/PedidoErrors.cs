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
}
