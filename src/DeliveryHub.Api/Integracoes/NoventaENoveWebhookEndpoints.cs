using DeliveryHub.Infrastructure.Integrations.DiDiFood;
using DeliveryHub.Infrastructure.Integrations.DiDiFood.Polling;
using DeliveryHub.Infrastructure.Persistence;

namespace DeliveryHub.Api.Integracoes;

// Porta de entrada dos pedidos da DiDi Food (99Food no Brasil).
// Transporte push: a DiDi chama este endpoint quando um pedido muda de estado.
//
// Este endpoint nunca importa DeliveryHub.Infrastructure.Integrations.DiDiFood
// .Contracts — todo o contrato interno (DiDiWebhookEvent, DiDiOrderModel) fica
// atrás de IDiDiFoodWebhookGateway. É a mesma fronteira ACL que a CLAUDE.md §4
// já exige entre o domínio e o marketplace, estendida aqui para a Api: quem
// recebe o webhook não precisa (e não deve) conhecer o formato de ninguém.
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
        IDiDiFoodWebhookGateway gateway,
        INoventaENoveMerchantResolver merchantResolver,
        ILoggerFactory logs,
        CancellationToken ct)
    {
        var log = logs.CreateLogger("Webhook99Food");

        // Lê o corpo uma vez — HttpRequest.Body não é rewind.
        using var leitor = new StreamReader(request.Body, System.Text.Encoding.UTF8);
        var corpo = await leitor.ReadToEndAsync(ct);

        if (string.IsNullOrWhiteSpace(corpo))
            return Erro("corpo vazio");

        var recebido = gateway.Validar(corpo);
        if (recebido is null)
        {
            log.LogWarning("Webhook da 99Food recusado: JSON ilegível ou assinatura inválida.");
            return Erro("assinatura inválida");
        }

        var merchantId = recebido.AppShopId is null
            ? null
            : await merchantResolver.ResolverAsync(recebido.AppShopId, ct);

        var gravado = await inbox.TentarGravarAsync(
            Origem,
            recebido.EventId,
            externalMerchantId: Guid.Empty,
            merchantId: merchantId,
            payloadJson: corpo,
            ct);

        log.LogInformation(
            "Webhook 99Food recebido. event={EventType} order={OrderId} novo={Novo}.",
            recebido.EventType, recebido.OrderId, gravado);

        // Processa o inbox em segundo plano — grava e reconhece rápido,
        // processa depois (CLAUDE.md §7: desacoplar ingestão de processamento).
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

        return Ok();
    }

    // Envelope exigido pelo guia da 99Food ("Webhook de Pedidos"): sem esta
    // resposta exata, o evento pode ser tratado como não processado do lado
    // deles, e o pedido nunca avança. HTTP 200 nos dois casos — a doc só
    // documenta o corpo, não distingue status HTTP para erro de validação.
    private static IResult Ok() => Results.Json(new { errno = 0, errmsg = "ok" });

    private static IResult Erro(string motivo) => Results.Json(new { errno = 1, errmsg = motivo });
}
