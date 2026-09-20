using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.SharedKernel;
using DeliveryHub.Infrastructure.Integrations.DiDiFood.Orders;
using DeliveryHub.Infrastructure.Integrations.IFood;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeliveryHub.Infrastructure.Integrations.DiDiFood;

// Adapter da porta IOrderSource para a 99Food / DiDi Food (Hexagonal Architecture).
internal sealed class DiDiFoodOrderSource : IOrderSource
{
    private readonly IDiDiFoodOrderClient _client;
    private readonly IOptions<DiDiFoodOptions> _opcoes;
    private readonly ILogger<DiDiFoodOrderSource> _logger;

    public DiDiFoodOrderSource(
        IDiDiFoodOrderClient client,
        IOptions<DiDiFoodOptions> opcoes,
        ILogger<DiDiFoodOrderSource> logger)
    {
        _client = client;
        _opcoes = opcoes;
        _logger = logger;
    }

    public Task<Result> ConfirmarPedidoAsync(string idExternoPedido, CancellationToken ct) =>
        ExecutarAsync(idExternoPedido, "confirmar", (id, token, cancellationToken) => _client.ConfirmOrderAsync(id, token, cancellationToken), ct);

    public Task<Result> IniciarPreparoAsync(string idExternoPedido, CancellationToken ct) =>
        // 99Food confirma e inicia preparo no mesmo fluxo — no-op no preparo se já confirmado
        Task.FromResult(Result.Success());

    public Task<Result> MarcarProntoAsync(string idExternoPedido, CancellationToken ct) =>
        ExecutarAsync(idExternoPedido, "marcar pronto", (id, token, cancellationToken) => _client.OrderReadyAsync(id, token, cancellationToken), ct);

    public Task<Result> DespacharAsync(string idExternoPedido, CancellationToken ct) =>
        ExecutarAsync(idExternoPedido, "despachar", (id, token, cancellationToken) => _client.OrderDeliveredAsync(id, token, cancellationToken), ct);

    public Task<Result<bool>> VerificarCodigoDeEntregaAsync(
        string idExternoPedido, string codigo, CancellationToken ct)
    {
        // 99Food não requer código de entrega de 4 dígitos como o iFood
        return Task.FromResult(Result.Success(true));
    }

    private async Task<Result> ExecutarAsync(
        string idExternoPedido,
        string descricao,
        Func<long, string, CancellationToken, Task> acao,
        CancellationToken ct)
    {
        if (!long.TryParse(idExternoPedido, out var orderId))
            return Result.Failure(OrderSourceErrors.OrigemRecusou);

        var authToken = _opcoes.Value.AppSecret ?? string.Empty;

        try
        {
            await acao(orderId, authToken, ct);
            return Result.Success();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Falha de rede ao executar '{Acao}' na 99Food para o pedido {PedidoId}.", descricao, idExternoPedido);
            return Result.Failure(OrderSourceErrors.OrigemIndisponivel);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "99Food recusou '{Acao}' no pedido {PedidoId}.", descricao, idExternoPedido);
            return Result.Failure(OrderSourceErrors.OrigemRecusou);
        }
    }
}
