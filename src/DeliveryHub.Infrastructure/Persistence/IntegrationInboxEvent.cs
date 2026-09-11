namespace DeliveryHub.Infrastructure.Persistence;

// Inbox de ingestão. A deduplicação é a constraint UNIQUE (source,
// external_event_id) — nunca um SELECT antes do INSERT, que é race condition.
public sealed class IntegrationInboxEvent
{
    private IntegrationInboxEvent() { }

    public Guid Id { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public string ExternalEventId { get; private set; } = string.Empty;

    // Id da loja na origem. Sempre presente no evento do iFood.
    public Guid ExternalMerchantId { get; private set; }

    // Nosso tenant. Nulo quando a loja ainda não existe na nossa base: o evento
    // fica em quarentena, reconhecido ao iFood mas não processado, para poder
    // ser reprocessado depois que o merchant for cadastrado.
    public Guid? MerchantId { get; private set; }

    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    public bool EmQuarentena => MerchantId is null;

    public static IntegrationInboxEvent Receber(
        string source,
        string externalEventId,
        Guid externalMerchantId,
        Guid? merchantId,
        string payload,
        DateTimeOffset receivedAt) => new()
        {
            Id = Guid.CreateVersion7(),
            Source = source,
            ExternalEventId = externalEventId,
            ExternalMerchantId = externalMerchantId,
            MerchantId = merchantId,
            Payload = payload,
            ReceivedAt = receivedAt
        };
}
