using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.SharedKernel;
using DeliveryHub.Infrastructure.Integrations.DiDiFood.Auth;
using DeliveryHub.Infrastructure.Integrations.DiDiFood.Orders;
using DeliveryHub.Infrastructure.Integrations.IFood;
using DeliveryHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeliveryHub.Infrastructure.Integrations.DiDiFood;

// Adapter da porta IOrderSource para a 99Food / DiDi Food.
//
// O auth_token da 99Food é por loja ("retorna o auth_token para a loja
// solicitada"), não um token único do app como no iFood Centralizado — por
// isso toda ação aqui precisa primeiro descobrir de qual loja é o pedido.
// idExternoPedido é só o order_id numérico da 99Food; a ponte até o
// app_shop_id é o próprio Pedido já persistido (Infrastructure lendo
// AppDbContext direto, mesmo padrão que o DiDiFoodInboxProcessor já usa nesta
// integração — não uma regra nova).
internal sealed class DiDiFoodOrderSource : IOrderSource
{
    private readonly IDiDiFoodOrderClient _client;
    private readonly IDiDiFoodAuthenticator _authenticator;
    private readonly AppDbContext _db;
    private readonly ILogger<DiDiFoodOrderSource> _logger;

    public DiDiFoodOrderSource(
        IDiDiFoodOrderClient client,
        IDiDiFoodAuthenticator authenticator,
        AppDbContext db,
        ILogger<DiDiFoodOrderSource> logger)
    {
        _client = client;
        _authenticator = authenticator;
        _db = db;
        _logger = logger;
    }

    public Task<Result> ConfirmarPedidoAsync(string idExternoPedido, CancellationToken ct) =>
        ExecutarAsync(idExternoPedido, "confirmar",
            (id, token, cancellationToken) => _client.ConfirmOrderAsync(id, token, cancellationToken), ct);

    public Task<Result> IniciarPreparoAsync(string idExternoPedido, CancellationToken ct) =>
        // 99Food confirma e inicia preparo no mesmo fluxo — no-op no preparo se já confirmado.
        Task.FromResult(Result.Success());

    public Task<Result> MarcarProntoAsync(string idExternoPedido, CancellationToken ct) =>
        ExecutarAsync(idExternoPedido, "marcar pronto",
            (id, token, cancellationToken) => _client.OrderReadyAsync(id, token, cancellationToken), ct);

    public async Task<Result> DespacharAsync(string idExternoPedido, CancellationToken ct)
    {
        if (!long.TryParse(idExternoPedido, out var orderId))
            return Result.Failure(OrderSourceErrors.OrigemRecusou);

        var contexto = await ObterContextoAsync(orderId, ct);
        if (contexto is null)
            return Result.Failure(OrderSourceErrors.OrigemRecusou);

        // Fluxo do guia: "Entrega loja: confirm > ready > delivered" contra
        // "Entrega 99: confirm > ready" — só chamamos /delivered quando o
        // motoboy é nosso. Quando é da 99, a própria plataforma finaliza a
        // entrega e reporta de volta pelo webhook (orderFinish); chamar
        // /delivered aqui seria notificar uma etapa que não é nossa.
        if (contexto.Value.EntregaPeloParceiro)
            return Result.Success();

        return await ExecutarComContextoAsync(
            orderId, contexto.Value.AppShopId, "despachar",
            (id, token, cancellationToken) => _client.OrderDeliveredAsync(id, token, cancellationToken), ct);
    }

    public Task<Result> CancelarPedidoAsync(string idExternoPedido, string motivo, CancellationToken ct)
    {
        // 99Food (DiDiFood) está pausado (CLAUDE.md §9 / diretriz do produto).
        // Cancelamento na origem da 99 fica como no-op de sucesso até o escopo
        // ser retomado — o cancelamento local no domínio segue valendo.
        return Task.FromResult(Result.Success());
    }

    public Task<Result<bool>> VerificarCodigoDeEntregaAsync(
        string idExternoPedido, string codigo, CancellationToken ct)
    {
        // A 99Food não usa um código de 4 dígitos validado pela nossa API como
        // o iFood — a confirmação de entrega própria da loja acontece pelo
        // link handover_page_url que o próprio motoboy acessa (guia,
        // "Localizador de Pedidos"). Nada para verificar deste lado.
        return Task.FromResult(Result.Success(true));
    }

    private Task<Result> ExecutarAsync(
        string idExternoPedido,
        string descricao,
        Func<long, string, CancellationToken, Task> acao,
        CancellationToken ct) =>
        long.TryParse(idExternoPedido, out var orderId)
            ? ExecutarComOrderIdAsync(orderId, descricao, acao, ct)
            : Task.FromResult(Result.Failure(OrderSourceErrors.OrigemRecusou));

    private async Task<Result> ExecutarComOrderIdAsync(
        long orderId,
        string descricao,
        Func<long, string, CancellationToken, Task> acao,
        CancellationToken ct)
    {
        var contexto = await ObterContextoAsync(orderId, ct);
        if (contexto is null)
            return Result.Failure(OrderSourceErrors.OrigemRecusou);

        return await ExecutarComContextoAsync(orderId, contexto.Value.AppShopId, descricao, acao, ct);
    }

    private async Task<Result> ExecutarComContextoAsync(
        long orderId,
        string? appShopId,
        string descricao,
        Func<long, string, CancellationToken, Task> acao,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(appShopId))
        {
            _logger.LogWarning(
                "Pedido 99Food {PedidoId} sem loja vinculada (NoventaENoveAppShopId) — não é possível autenticar.",
                orderId);
            return Result.Failure(OrderSourceErrors.OrigemRecusou);
        }

        try
        {
            var authToken = await _authenticator.GetAuthTokenAsync(appShopId, ct);
            await acao(orderId, authToken, ct);
            return Result.Success();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Falha de rede ao executar '{Acao}' na 99Food para o pedido {PedidoId}.", descricao, orderId);
            return Result.Failure(OrderSourceErrors.OrigemIndisponivel);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "99Food recusou '{Acao}' no pedido {PedidoId}.", descricao, orderId);
            return Result.Failure(OrderSourceErrors.OrigemRecusou);
        }
    }

    // Pedido → Merchant num round-trip só. AsNoTracking: leitura de apoio a
    // uma chamada de API, não algo que vai ser salvo por aqui.
    private async Task<(string? AppShopId, bool EntregaPeloParceiro)?> ObterContextoAsync(long orderId, CancellationToken ct)
    {
        var idExterno = orderId.ToString();

        var linha = await _db.Pedidos
            .AsNoTracking()
            .Where(x => x.IdExterno == idExterno)
            .Select(x => new { x.MerchantId, x.EntregaPeloParceiro })
            .FirstOrDefaultAsync(ct);

        if (linha is null)
            return null;

        var appShopId = await _db.Merchants
            .AsNoTracking()
            .Where(x => x.Id == linha.MerchantId)
            .Select(x => x.NoventaENoveAppShopId)
            .FirstOrDefaultAsync(ct);

        return (appShopId, linha.EntregaPeloParceiro);
    }
}
