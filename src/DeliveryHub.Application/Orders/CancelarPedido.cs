using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Orders;
using DeliveryHub.Domain.SharedKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;

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
    private readonly ILogger<CancelarPedido> _logger;

    public CancelarPedido(
        IPedidoRepository pedidos,
        IOrderSource origem,
        INotificadorPainel notificador,
        TimeProvider relogio,
        ILogger<CancelarPedido>? logger = null)
    {
        _pedidos = pedidos;
        _origem = origem;
        _notificador = notificador;
        _relogio = relogio;
        _logger = logger ?? NullLogger<CancelarPedido>.Instance;
    }

    public async Task<Result> ExecutarAsync(Guid pedidoId, string motivo, CancellationToken ct)
    {
        var duracao = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(motivo))
        {
            _logger.LogWarning("Cancelamento de pedido recusado: motivo ausente. PedidoId={PedidoId} Erro={Erro}",
                pedidoId, PedidoErrors.MotivoCancelamentoObrigatorio.Code);
            return Result.Failure(PedidoErrors.MotivoCancelamentoObrigatorio);
        }

        var pedido = await _pedidos.ObterPorIdAsync(pedidoId, ct);
        if (pedido is null)
        {
            _logger.LogWarning("Cancelamento de pedido recusado: pedido não encontrado. PedidoId={PedidoId}", pedidoId);
            return Result.Failure(AvancarPedidoErrors.PedidoNaoEncontrado);
        }

        var texto = motivo.Trim();
        var statusAnterior = pedido.Status;

        // Origem primeiro: só marca cancelado localmente depois que o
        // marketplace aceitou, senão o lojista veria um estado que não existe
        // lá fora. Pedido nascido aqui dentro pula — não há marketplace a avisar.
        if (pedido.TemOrigemExterna)
        {
            var naOrigem = await _origem.CancelarPedidoAsync(pedido.IdExterno, texto, ct);
            if (naOrigem.IsFailure)
            {
                _logger.LogWarning("Origem recusou cancelamento do pedido. PedidoId={PedidoId} MerchantId={MerchantId} Erro={Erro} DuraçãoMs={DuracaoMs}",
                    pedido.Id, pedido.MerchantId, naOrigem.Error.Code, duracao.Elapsed.TotalMilliseconds);
                return naOrigem;
            }
        }

        var cancelamento = pedido.Cancelar(texto, _relogio.GetUtcNow());
        if (cancelamento.IsFailure)
        {
            _logger.LogWarning("Domínio recusou cancelamento do pedido. PedidoId={PedidoId} MerchantId={MerchantId} Status={Status} Erro={Erro}",
                pedido.Id, pedido.MerchantId, statusAnterior, cancelamento.Error.Code);
            return cancelamento;
        }

        await _pedidos.SalvarAsync(ct);
        await _notificador.ResumoAtualizadoAsync(pedido.MerchantId, ct);
        _logger.LogInformation("Pedido cancelado. PedidoId={PedidoId} MerchantId={MerchantId} StatusAnterior={StatusAnterior} StatusNovo={StatusNovo} OrigemExterna={OrigemExterna} DuraçãoMs={DuracaoMs}",
            pedido.Id, pedido.MerchantId, statusAnterior, pedido.Status, pedido.TemOrigemExterna, duracao.Elapsed.TotalMilliseconds);
        return Result.Success();
    }
}
