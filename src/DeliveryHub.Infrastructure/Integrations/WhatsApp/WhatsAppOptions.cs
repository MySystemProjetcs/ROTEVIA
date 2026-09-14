namespace DeliveryHub.Infrastructure.Integrations.WhatsApp;

public sealed class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    // O worker Node (whatsapp-worker/) que segura a sessão do whatsapp-web.js.
    public string WorkerBaseUrl { get; init; } = "http://localhost:3002";

    // Segredo que o worker manda em X-Zap-Secret nos webhooks. Só em
    // user-secrets — nunca em appsettings.json (CLAUDE.md §10).
    public string WebhookSecret { get; init; } = string.Empty;
}
