using Microsoft.EntityFrameworkCore;

namespace DeliveryHub.Infrastructure.Persistence;

public interface IIntegrationInboxWriter
{
    // Devolve false quando o evento já estava no inbox. Não é erro: é o caminho
    // normal de reentrega do iFood, e o chamador deve reconhecer e seguir.
    Task<bool> TentarGravarAsync(
        string source,
        string externalEventId,
        Guid externalMerchantId,
        Guid? merchantId,
        string payloadJson,
        CancellationToken ct);
}

internal sealed class IntegrationInboxWriter : IIntegrationInboxWriter
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public IntegrationInboxWriter(AppDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<bool> TentarGravarAsync(
        string source,
        string externalEventId,
        Guid externalMerchantId,
        Guid? merchantId,
        string payloadJson,
        CancellationToken ct)
    {
        // Verificação e inserção precisam ser a mesma operação atômica: dois
        // workers passando por um SELECT antes do INSERT inserem os dois
        // (ENGINEERING-GUIDE §4). Quem deduplica é a constraint ux_inbox_event.
        var linhas = await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO integration_inbox
                (id, source, external_event_id, external_merchant_id, merchant_id, payload, received_at)
            VALUES
                ({Guid.CreateVersion7()}, {source}, {externalEventId}, {externalMerchantId},
                 {merchantId}, {payloadJson}::jsonb, {_timeProvider.GetUtcNow()})
            ON CONFLICT (source, external_event_id) DO NOTHING
            """, ct);

        return linhas > 0;
    }
}
