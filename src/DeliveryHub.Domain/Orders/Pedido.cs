using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Domain.Orders;

public sealed class Pedido : ITenantOwned
{
    private Pedido(Guid id, Guid merchantId, string idExterno, bool ehTeste, DateTimeOffset recebidoEm)
    {
        Id = id;
        MerchantId = merchantId;
        IdExterno = idExterno;
        EhTeste = ehTeste;
        RecebidoEm = recebidoEm;
        Status = StatusPedido.Recebido;
    }

    public Guid Id { get; }
    public Guid MerchantId { get; }

    // Id do pedido na origem. É por ele que a ingestão deduplica.
    public string IdExterno { get; }

    // Vem do isTest do payload do iFood. Precisa sobreviver até o Ledger:
    // pedido de sandbox não pode virar lançamento financeiro.
    public bool EhTeste { get; }

    public DateTimeOffset RecebidoEm { get; }
    public StatusPedido Status { get; private set; }

    public static Pedido Receber(Guid merchantId, string idExterno, bool ehTeste, DateTimeOffset recebidoEm) =>
        new(Guid.CreateVersion7(), merchantId, idExterno, ehTeste, recebidoEm);

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
