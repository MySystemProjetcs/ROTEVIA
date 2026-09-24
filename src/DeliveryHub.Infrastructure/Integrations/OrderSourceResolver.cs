using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Orders;
using DeliveryHub.Domain.SharedKernel;
using DeliveryHub.Infrastructure.Integrations.DiDiFood;
using DeliveryHub.Infrastructure.Integrations.IFood;

namespace DeliveryHub.Infrastructure.Integrations;

// Design Pattern: Strategy / Roteador de IOrderSource (Open-Closed Principle).
// Inspeciona o idExterno do pedido para direcionar a ação ao marketplace correto
// (iFood, 99Food ou Local) sem que a camada de Aplicação precise saber detalhes.
internal sealed class OrderSourceResolver : IOrderSource
{
    private readonly IFoodOrderSource _iFoodSource;
    private readonly DiDiFoodOrderSource _diDiFoodSource;

    public OrderSourceResolver(
        IFoodOrderSource iFoodSource,
        DiDiFoodOrderSource diDiFoodSource)
    {
        _iFoodSource = iFoodSource;
        _diDiFoodSource = diDiFoodSource;
    }

    public Task<Result> ConfirmarPedidoAsync(string idExternoPedido, CancellationToken ct) =>
        ObterSource(idExternoPedido).ConfirmarPedidoAsync(idExternoPedido, ct);

    public Task<Result> IniciarPreparoAsync(string idExternoPedido, CancellationToken ct) =>
        ObterSource(idExternoPedido).IniciarPreparoAsync(idExternoPedido, ct);

    public Task<Result> MarcarProntoAsync(string idExternoPedido, CancellationToken ct) =>
        ObterSource(idExternoPedido).MarcarProntoAsync(idExternoPedido, ct);

    public Task<Result> DespacharAsync(string idExternoPedido, CancellationToken ct) =>
        ObterSource(idExternoPedido).DespacharAsync(idExternoPedido, ct);

    public Task<Result> CancelarPedidoAsync(string idExternoPedido, string motivo, CancellationToken ct) =>
        ObterSource(idExternoPedido).CancelarPedidoAsync(idExternoPedido, motivo, ct);

    public Task<Result<bool>> VerificarCodigoDeEntregaAsync(
        string idExternoPedido, string codigo, CancellationToken ct) =>
        ObterSource(idExternoPedido).VerificarCodigoDeEntregaAsync(idExternoPedido, codigo, ct);

    private IOrderSource ObterSource(string idExternoPedido)
    {
        if (string.IsNullOrWhiteSpace(idExternoPedido) || idExternoPedido.StartsWith(Pedido.PrefixoOrigemLocal, StringComparison.Ordinal))
            return NullOrderSource.Instance;

        // Id de pedido do 99Food é numérico (long, ex: 2352921557674426622)
        if (long.TryParse(idExternoPedido, out _) || idExternoPedido.StartsWith("99food-", StringComparison.OrdinalIgnoreCase))
            return _diDiFoodSource;

        // Default: iFood (IDs formato GUID)
        return _iFoodSource;
    }

    private sealed class NullOrderSource : IOrderSource
    {
        public static readonly NullOrderSource Instance = new();

        public Task<Result> ConfirmarPedidoAsync(string idExternoPedido, CancellationToken ct) => Task.FromResult(Result.Success());
        public Task<Result> IniciarPreparoAsync(string idExternoPedido, CancellationToken ct) => Task.FromResult(Result.Success());
        public Task<Result> MarcarProntoAsync(string idExternoPedido, CancellationToken ct) => Task.FromResult(Result.Success());
        public Task<Result> DespacharAsync(string idExternoPedido, CancellationToken ct) => Task.FromResult(Result.Success());
        public Task<Result> CancelarPedidoAsync(string idExternoPedido, string motivo, CancellationToken ct) => Task.FromResult(Result.Success());
        public Task<Result<bool>> VerificarCodigoDeEntregaAsync(string idExternoPedido, string codigo, CancellationToken ct) => Task.FromResult(Result.Success(true));
    }
}
