using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Orders;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Application.Orders;

public interface IConfirmarColetaComCodigo
{
    Task<Result> ExecutarAsync(
        Guid usuarioLogadoId, Guid pedidoId, string codigo, CancellationToken ct);
}

// Fluxo gêmeo do ConfirmarEntregaComCodigo, no momento oposto da corrida: aqui
// o motoboy está pegando o pedido na loja e confirma com o balcão o código de
// coleta antes de sair com ele. A origem (iFood) é quem valida — a nossa parte
// é só garantir que quem chama é o motoboy certo deste pedido.
//
// Sem efeito colateral no domínio: diferente da entrega, a coleta não tem
// status novo no agregado Pedido — a resposta "aceita" basta para o motoboy
// prosseguir. Se amanhã surgir um estado "Coletado" distinto de "Despachado",
// este caso de uso é o lugar onde ele seria aplicado.
public sealed class ConfirmarColetaComCodigo : IConfirmarColetaComCodigo
{
    private readonly IPedidoRepository _pedidos;
    private readonly ICourierRepository _couriers;
    private readonly IOrderSource _origem;

    public ConfirmarColetaComCodigo(
        IPedidoRepository pedidos,
        ICourierRepository couriers,
        IOrderSource origem)
    {
        _pedidos = pedidos;
        _couriers = couriers;
        _origem = origem;
    }

    public async Task<Result> ExecutarAsync(
        Guid usuarioLogadoId, Guid pedidoId, string codigo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return Result.Failure(PedidoErrors.CodigoDeEntregaInvalido);

        var courier = await _couriers.ObterPorUsuarioIdAsync(usuarioLogadoId, ct);
        if (courier is null)
            return Result.Failure(PedidoErrors.EntregadorNaoPertenceAoPedido);

        var pedido = await _pedidos.ObterParaEntregadorAsync(pedidoId, ct);

        // Mesma mensagem para "não existe" e "não é seu": não revela que o
        // pedido existe para quem não foi designado para ele.
        if (pedido is null || pedido.EntregadorId != courier.Id)
            return Result.Failure(PedidoErrors.EntregadorNaoPertenceAoPedido);

        var verificacao = await _origem.ValidarCodigoDeColetaAsync(
            pedido.IdExterno, codigo.Trim(), ct);

        if (verificacao.IsFailure)
            return Result.Failure(verificacao.Error);

        if (!verificacao.Value)
            return Result.Failure(PedidoErrors.CodigoDeEntregaInvalido);

        return Result.Success();
    }
}
