using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Orders;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Application.Orders;

public interface ICancelarPedido
{
    Task<Result> ExecutarAsync(Guid pedidoId, string motivo, CancellationToken ct);
}

// Cancelamento pelo lojista, alcançável em qualquer status não terminal. Mesmo
// arranjo do AvancarPedido: avisa a origem primeiro, depois move o domínio — um
// cancelamento local sem a origem saber deixaria o pedido "vivo" no marketplace.
// Motivo é obrigatório aqui (regra de negócio), embora o domínio o aceite nulo
// para o caminho do evento CANCELLED que chega do próprio iFood.
public sealed class CancelarPedido : ICancelarPedido
{
    private readonly IPedidoRepository _pedidos;
    private readonly IOrderSource _origem;
    private readonly INotificadorPainel _notificador;
    private readonly TimeProvider _relogio;

    public CancelarPedido(
        IPedidoRepository pedidos,
        IOrderSource origem,
        INotificadorPainel notificador,
        TimeProvider relogio)
    {
        _pedidos = pedidos;
        _origem = origem;
        _notificador = notificador;
        _relogio = relogio;
    }

    public async Task<Result> ExecutarAsync(Guid pedidoId, string motivo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            return Result.Failure(PedidoErrors.MotivoCancelamentoObrigatorio);

        var pedido = await _pedidos.ObterPorIdAsync(pedidoId, ct);
        if (pedido is null)
            return Result.Failure(AvancarPedidoErrors.PedidoNaoEncontrado);

        var texto = motivo.Trim();

        // Origem primeiro: só marca cancelado localmente depois que o
        // marketplace aceitou, senão o lojista veria um estado que não existe
        // lá fora. Pedido nascido aqui dentro pula — não há marketplace a avisar.
        if (pedido.TemOrigemExterna)
        {
            var naOrigem = await _origem.CancelarPedidoAsync(pedido.IdExterno, texto, ct);
            if (naOrigem.IsFailure)
                return naOrigem;
        }

        var cancelamento = pedido.Cancelar(texto, _relogio.GetUtcNow());
        if (cancelamento.IsFailure)
            return cancelamento;

        await _pedidos.SalvarAsync(ct);
        await _notificador.ResumoAtualizadoAsync(pedido.MerchantId, ct);
        return Result.Success();
    }
}
