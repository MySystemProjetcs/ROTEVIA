using System.Security.Cryptography;
using System.Text;
using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Merchants;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using DeliveryHub.Infrastructure.Integrations.WhatsApp;

namespace DeliveryHub.Api.WhatsApp;

public sealed record StatusWhatsAppResponse(string Status, string? QrCodeBase64, string? Telefone, string? Erro);

public sealed record WebhookStatusSessaoRequest(Guid MerchantId, string Status, string? QrDataUrl, string? Telefone, string? Erro);

public sealed record WebhookMensagemRecebidaRequest(Guid MerchantId, string De, string Corpo, string ExternalId, DateTimeOffset OcorridoEm);

public static class WhatsAppEndpoints
{
    public static void MapWhatsAppEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/restaurantes")
            .WithTags("WhatsApp")
            .RequireAuthorization(Identity.Policies.OperadorDaLoja);

        group.MapPost("/{merchantId:guid}/whatsapp/conectar", Conectar);
        group.MapGet("/{merchantId:guid}/whatsapp/status", ObterStatus);
        group.MapPost("/{merchantId:guid}/whatsapp/desconectar", Desconectar);

        // Sem sessão: quem chama é o worker Node, não o navegador do dono. A
        // prova de identidade é o X-Zap-Secret, verificado dentro do handler
        // (não dá pra expressar "cabeçalho correto" como policy declarativa).
        var webhooks = app.MapGroup("/api/webhooks/whatsapp").WithTags("WhatsApp").AllowAnonymous();

        webhooks.MapPost("/session", WebhookStatusDaSessao);
        webhooks.MapPost("/", WebhookMensagemRecebida);
    }

    private static async Task<Results<Ok<StatusWhatsAppResponse>, ProblemHttpResult>> Conectar(
        Guid merchantId, ITenantContext tenant, IWhatsAppWorkerClient worker, IMerchantRepository merchants, CancellationToken ct)
    {
        if (!PodeAcessar(tenant, merchantId))
            return TypedResults.Problem(title: "Restaurante não encontrado.", statusCode: StatusCodes.Status404NotFound);

        var status = await worker.ConectarAsync(merchantId, ct);
        await PersistirStatusAsync(merchants, merchantId, status, ct);

        return TypedResults.Ok(ParaResposta(status));
    }

    private static async Task<Results<Ok<StatusWhatsAppResponse>, ProblemHttpResult>> ObterStatus(
        Guid merchantId, ITenantContext tenant, IWhatsAppWorkerClient worker, IMerchantRepository merchants, CancellationToken ct)
    {
        if (!PodeAcessar(tenant, merchantId))
            return TypedResults.Problem(title: "Restaurante não encontrado.", statusCode: StatusCodes.Status404NotFound);

        var status = await worker.ObterStatusAsync(merchantId, ct);

        // O worker guarda a sessão em disco (LocalAuth), mas o estado em
        // memória dele some se o processo reiniciar. Se o banco ainda diz
        // conectado e o worker diz desconectado, tenta restaurar sozinho —
        // sem isso o dono precisaria clicar "Conectar" de novo à toa.
        if (MapearStatus(status.Status) == StatusConexaoWhatsApp.Desconectado)
        {
            var merchant = await merchants.ObterPorIdAsync(merchantId, ct);
            if (merchant?.ConexaoWhatsApp?.Status == StatusConexaoWhatsApp.Conectado)
                status = await worker.ConectarAsync(merchantId, ct);
        }

        await PersistirStatusAsync(merchants, merchantId, status, ct);
        return TypedResults.Ok(ParaResposta(status));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Desconectar(
        Guid merchantId, ITenantContext tenant, IWhatsAppWorkerClient worker, IMerchantRepository merchants, CancellationToken ct)
    {
        if (!PodeAcessar(tenant, merchantId))
            return TypedResults.Problem(title: "Restaurante não encontrado.", statusCode: StatusCodes.Status404NotFound);

        await worker.DesconectarAsync(merchantId, ct);
        await PersistirStatusAsync(merchants, merchantId, new StatusSessaoWorker("DISCONNECTED", null, null, null), ct);

        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, UnauthorizedHttpResult>> WebhookStatusDaSessao(
        WebhookStatusSessaoRequest request, HttpRequest http, IOptions<WhatsAppOptions> opcoes,
        IMerchantRepository merchants, CancellationToken ct)
    {
        if (!SegredoValido(http, opcoes.Value))
            return TypedResults.Unauthorized();

        var merchant = await merchants.ObterPorIdAsync(request.MerchantId, ct);
        if (merchant is null)
            return TypedResults.NoContent();

        var status = MapearStatus(request.Status);
        merchant.AtualizarStatusWhatsApp(status, request.Telefone, status == StatusConexaoWhatsApp.Conectado ? DateTimeOffset.UtcNow : null);
        await merchants.SalvarAsync(ct);

        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, UnauthorizedHttpResult>> WebhookMensagemRecebida(
        WebhookMensagemRecebidaRequest request, HttpRequest http, IOptions<WhatsAppOptions> opcoes,
        IMerchantRepository merchants, CancellationToken ct)
    {
        if (!SegredoValido(http, opcoes.Value))
            return TypedResults.Unauthorized();

        var merchant = await merchants.ObterPorIdAsync(request.MerchantId, ct);
        if (merchant is null)
            return TypedResults.NoContent();

        merchant.RegistrarMensagemWhatsApp(new MensagemWhatsApp(
            DirecaoMensagemWhatsApp.Recebida, request.De, request.Corpo, request.ExternalId, request.OcorridoEm));
        await merchants.SalvarAsync(ct);

        return TypedResults.NoContent();
    }

    private static bool SegredoValido(HttpRequest request, WhatsAppOptions opcoes)
    {
        var recebido = request.Headers["X-Zap-Secret"].ToString();
        if (string.IsNullOrEmpty(recebido) || string.IsNullOrEmpty(opcoes.WebhookSecret))
            return false;

        // Tempo constante: um webhook é a única porta anônima deste recurso,
        // então o segredo merece a mesma cautela que uma senha.
        var a = Encoding.UTF8.GetBytes(recebido);
        var b = Encoding.UTF8.GetBytes(opcoes.WebhookSecret);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static async Task PersistirStatusAsync(
        IMerchantRepository merchants, Guid merchantId, StatusSessaoWorker status, CancellationToken ct)
    {
        var merchant = await merchants.ObterPorIdAsync(merchantId, ct);
        if (merchant is null) return;

        var statusMapeado = MapearStatus(status.Status);
        merchant.AtualizarStatusWhatsApp(
            statusMapeado, status.Telefone, statusMapeado == StatusConexaoWhatsApp.Conectado ? DateTimeOffset.UtcNow : null);

        await merchants.SalvarAsync(ct);
    }

    private static StatusConexaoWhatsApp MapearStatus(string statusWorker) => statusWorker switch
    {
        "QR_PENDING" or "SCANNING" => StatusConexaoWhatsApp.AguardandoLeituraDoQr,
        "CONNECTED" => StatusConexaoWhatsApp.Conectado,
        "ERROR" => StatusConexaoWhatsApp.Erro,
        _ => StatusConexaoWhatsApp.Desconectado
    };

    private static bool PodeAcessar(ITenantContext tenant, Guid merchantId) =>
        tenant.PodeVerTodosOsTenants || tenant.MerchantId == merchantId;

    // QrDataUrl já vem pronto do worker ("data:image/png;base64,...") — o
    // campo se chama QrCodeBase64 só por compatibilidade com o contrato que a
    // SPA já consome, não porque o conteúdo ainda seja base64 cru.
    private static StatusWhatsAppResponse ParaResposta(StatusSessaoWorker status) =>
        new(MapearStatus(status.Status).ToString(), status.QrDataUrl, status.Telefone, status.Erro);
}
