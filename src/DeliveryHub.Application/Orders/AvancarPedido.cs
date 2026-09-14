using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Orders;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Application.Orders;

public enum AcaoDePedido
{
    Confirmar,
    IniciarPreparo,
    MarcarPronto,
    Despachar
}

public static class AvancarPedidoErrors
{
    public static readonly Error PedidoNaoEncontrado = new(
        "pedido.nao_encontrado",
        "Pedido não encontrado.",
        ErrorType.NotFound);
}

public interface IAvancarPedido
{
    Task<Result> ExecutarAsync(Guid pedidoId, AcaoDePedido acao, CancellationToken ct);
}

// Um caso de uso parametrizado em vez de quatro quase idênticos: o fluxo é o
// mesmo — avisa a origem, depois move o pedido —, só muda o alvo.
public sealed class AvancarPedido : IAvancarPedido
{
    private readonly IPedidoRepository _pedidos;
    private readonly IOrderSource _origem;
    private readonly INotificadorPainel _notificador;

    public AvancarPedido(IPedidoRepository pedidos, IOrderSource origem, INotificadorPainel notificador)
    {
        _pedidos = pedidos;
        _origem = origem;
        _notificador = notificador;
    }

    public async Task<Result> ExecutarAsync(Guid pedidoId, AcaoDePedido acao, CancellationToken ct)
    {
        var pedido = await _pedidos.ObterPorIdAsync(pedidoId, ct);
        if (pedido is null)
            return Result.Failure(AvancarPedidoErrors.PedidoNaoEncontrado);

        // Checa a pré-condição do domínio antes de avisar a origem: sem isso
        // o iFood saberia que o pedido "saiu" mesmo sem motoboy alocado, e só
        // depois a gente descobriria que o domínio recusa.
        if (acao == AcaoDePedido.Despachar && pedido.EntregadorId is null)
            return Result.Failure(PedidoErrors.SemEntregadorAlocado);

        // A origem primeiro: mudar o status local antes de o marketplace
        // aceitar deixaria o lojista vendo um estado que não existe lá fora.
        //
        // Pedido nascido aqui dentro pula esse passo — não há marketplace a
        // avisar, e chamar a API do iFood com um id que não existe lá só
        // devolveria recusa para uma venda legítima.
        if (pedido.TemOrigemExterna)
        {
            var naOrigem = await NotificarOrigemAsync(acao, pedido.IdExterno, ct);
            if (naOrigem.IsFailure)
                return naOrigem;
        }

        var transicao = AplicarNoDominio(acao, pedido);
        if (transicao.IsFailure)
            return transicao;

        await _pedidos.SalvarAsync(ct);
        await _notificador.ResumoAtualizadoAsync(pedido.MerchantId, ct);
        return Result.Success();
    }

    private Task<Result> NotificarOrigemAsync(AcaoDePedido acao, string idExterno, CancellationToken ct) => acao switch
    {
        AcaoDePedido.Confirmar => _origem.ConfirmarPedidoAsync(idExterno, ct),
        AcaoDePedido.IniciarPreparo => _origem.IniciarPreparoAsync(idExterno, ct),
        AcaoDePedido.MarcarPronto => _origem.MarcarProntoAsync(idExterno, ct),
        AcaoDePedido.Despachar => _origem.DespacharAsync(idExterno, ct),
        _ => throw new ArgumentOutOfRangeException(nameof(acao)),
    };

    private static Result AplicarNoDominio(AcaoDePedido acao, Pedido pedido) => acao switch
    {
        AcaoDePedido.Confirmar => pedido.Confirmar(),
        AcaoDePedido.IniciarPreparo => pedido.IniciarPreparo(),
        AcaoDePedido.MarcarPronto => pedido.MarcarPronto(),
        AcaoDePedido.Despachar => pedido.Despachar(),
        _ => throw new ArgumentOutOfRangeException(nameof(acao)),
    };
}
