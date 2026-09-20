using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeliveryHub.Infrastructure.Integrations.DiDiFood;
using DeliveryHub.Infrastructure.Integrations.DiDiFood.Contracts;
using DeliveryHub.Infrastructure.Integrations.DiDiFood.Polling;
using DeliveryHub.Infrastructure.Integrations.IFood;
using DeliveryHub.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace DeliveryHub.Api.Integracoes;

// Porta de entrada dos pedidos da DiDi Food (99Food no Brasil).
// Transporte push: a DiDi chama este endpoint quando um pedido muda de estado.
//
// Autenticação: MD5(app_id + timestamp + app_secret) — spec §shop/list.
// O sign e o timestamp chegam no corpo JSON, não em headers.
public static class NoventaENoveWebhookEndpoints
{
    public const string Origem = "99food";

    public static void MapNoventaENoveWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/webhooks/99food", Receber)
            .WithTags("99Food")
            .AllowAnonymous();
    }

    private static async Task<IResult> Receber(
        HttpRequest request,
        IIntegrationInboxWriter inbox,
        IDiDiFoodInboxProcessor processor,
        IMerchantResolver merchantResolver,
        IOptions<DiDiFoodOptions> opcoes,
        ILoggerFactory logs,
        CancellationToken ct)
    {
        var log = logs.CreateLogger("Webhook99Food");

        // Lê o corpo uma vez — HttpRequest.Body não é rewind
        using var leitor = new StreamReader(request.Body, Encoding.UTF8);
        var corpo = await leitor.ReadToEndAsync(ct);

        if (string.IsNullOrWhiteSpace(corpo))
            return Results.BadRequest();

        DiDiWebhookEvent? evento;
        try
        {
            evento = JsonSerializer.Deserialize<DiDiWebhookEvent>(corpo);
        }
        catch (JsonException ex)
        {
            log.LogWarning(ex, "Webhook da 99Food: JSON inválido.");
            return Results.BadRequest();
        }

        if (evento is null)
            return Results.BadRequest();

        // Verifica assinatura antes de qualquer processamento.
        // Fórmula da spec: MD5(app_id + timestamp + app_secret), hex uppercase.
        if (!AssinaturaValida(evento, opcoes.Value))
        {
            log.LogWarning(
                "Webhook da 99Food recusado: assinatura inválida. order_id={OrderId}",
                evento.OrderId);
            return Results.Unauthorized();
        }

        // Tenta resolver o merchant pelo AppShopId se fornecido
        var appShopId = evento.Order?.Shop?.AppShopId;
        Guid? merchantId = null;
        if (Guid.TryParse(appShopId, out var parsedShopId))
        {
            merchantId = await merchantResolver.ResolverAsync(parsedShopId, ct);
        }

        // event_id = combinação de event_type + order_id.
        var eventId = $"{evento.EventType}:{evento.OrderId}";

        var gravado = await inbox.TentarGravarAsync(
            Origem,
            eventId,
            externalMerchantId: Guid.Empty,
            merchantId: merchantId,
            payloadJson: corpo,
            ct);

        log.LogInformation(
            "Webhook 99Food recebido. event={EventType} order={OrderId} novo={Novo}.",
            evento.EventType, evento.OrderId, gravado);

        // Processa eventos pendentes do inbox do 99Food em segundo plano/assíncrono
        _ = Task.Run(async () =>
        {
            try
            {
                await processor.ProcessarPendentesAsync(limite: 10, CancellationToken.None);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Erro no processamento em background do inbox 99Food.");
            }
        }, CancellationToken.None);

        // 200 em todos os casos: evento duplicado não é erro, é reentrega normal.
        return Results.Ok();
    }

    // MD5(app_id + timestamp + app_secret) — mesmos parâmetros do /shop/list.
    // Comparação em tempo constante: webhook é porta anônima, timing attack
    // permitiria descobrir qual parte da assinatura está correta.
    private static bool AssinaturaValida(DiDiWebhookEvent evento, DiDiFoodOptions opcoes)
    {
        if (string.IsNullOrEmpty(evento.Sign) || string.IsNullOrEmpty(opcoes.AppSecret))
            return false;

        var entrada = $"{opcoes.AppId}{evento.Timestamp}{opcoes.AppSecret}";
        var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(entrada));
        var esperado = Convert.ToHexString(hashBytes); // uppercase

        var a = Encoding.UTF8.GetBytes(evento.Sign.ToUpperInvariant());
        var b = Encoding.UTF8.GetBytes(esperado);

        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}
