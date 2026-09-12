using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.SharedKernel;
using DeliveryHub.Infrastructure.Integrations.IFood.Orders;
using Microsoft.Extensions.Logging;

namespace DeliveryHub.Infrastructure.Integrations.IFood;

public static class OrderSourceErrors
{
    public static readonly Error OrigemRecusou = new(
        "origem.recusou_acao",
        "A origem do pedido recusou a operação.",
        ErrorType.Conflict);

    public static readonly Error OrigemIndisponivel = new(
        "origem.indisponivel",
        "Não foi possível falar com a origem do pedido.",
        ErrorType.Failure);
}

// Adapter da porta IOrderSource para o iFood. É aqui que a exceção da
// integração vira Result: falha de marketplace é caminho previsto para quem
// chama, não bug (ENGINEERING-GUIDE §3).
internal sealed class IFoodOrderSource : IOrderSource
{
    private readonly IIFoodOrderClient _client;
    private readonly ILogger<IFoodOrderSource> _logger;

    public IFoodOrderSource(IIFoodOrderClient client, ILogger<IFoodOrderSource> logger)
    {
        _client = client;
        _logger = logger;
    }

    public Task<Result> ConfirmarPedidoAsync(string idExternoPedido, CancellationToken ct) =>
        ExecutarAsync(idExternoPedido, "confirmar", _client.ConfirmAsync, ct);

    public Task<Result> IniciarPreparoAsync(string idExternoPedido, CancellationToken ct) =>
        ExecutarAsync(idExternoPedido, "iniciar preparo", _client.StartPreparationAsync, ct);

    public Task<Result> MarcarProntoAsync(string idExternoPedido, CancellationToken ct) =>
        ExecutarAsync(idExternoPedido, "marcar pronto", _client.ReadyToPickupAsync, ct);

    public Task<Result> DespacharAsync(string idExternoPedido, CancellationToken ct) =>
        ExecutarAsync(idExternoPedido, "despachar", _client.DispatchAsync, ct);

    private async Task<Result> ExecutarAsync(
        string idExternoPedido,
        string descricao,
        Func<Guid, CancellationToken, Task> acao,
        CancellationToken ct)
    {
        if (!Guid.TryParse(idExternoPedido, out var orderId))
            return Result.Failure(OrderSourceErrors.OrigemRecusou);

        try
        {
            await acao(orderId, ct);
            return Result.Success();
        }
        catch (IFoodApiException ex)
        {
            _logger.LogWarning(ex, "iFood recusou '{Acao}' no pedido {PedidoId}.", descricao, idExternoPedido);
            return Result.Failure(OrderSourceErrors.OrigemRecusou);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Falha de rede ao executar '{Acao}' no pedido {PedidoId}.", descricao, idExternoPedido);
            return Result.Failure(OrderSourceErrors.OrigemIndisponivel);
        }
    }
}
