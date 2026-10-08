using System.Collections.Concurrent;
using DeliveryHub.Application.Abstractions;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Merchants;

internal sealed class IFoodMerchantContextCache
{
    private static readonly TimeSpan SlidingTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan StaleRetryDelay = TimeSpan.FromSeconds(30);
    private readonly ConcurrentDictionary<Guid, CacheEntry> _entries = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();
    private readonly TimeProvider _timeProvider;

    public IFoodMerchantContextCache(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public SemaphoreSlim LockFor(Guid merchantId) => _locks.GetOrAdd(merchantId, _ => new SemaphoreSlim(1, 1));

    public bool TryGet(Guid merchantId, out IFoodMerchantContext context)
    {
        while (_entries.TryGetValue(merchantId, out var entry))
        {
            var now = _timeProvider.GetUtcNow();
            if (entry.ExpiresAt <= now)
            {
                _entries.TryRemove(new KeyValuePair<Guid, CacheEntry>(merchantId, entry));
                break;
            }

            var renewed = entry with { ExpiresAt = now.Add(SlidingTtl) };
            if (!_entries.TryUpdate(merchantId, renewed, entry))
                continue;

            context = ToContext(entry.Snapshot, entry.IsStale);
            return true;
        }

        context = default!;
        return false;
    }

    public bool RefreshIsDue(Guid merchantId)
    {
        if (!_entries.TryGetValue(merchantId, out var entry))
            return true;

        var now = _timeProvider.GetUtcNow();
        if (entry.ExpiresAt <= now)
        {
            _entries.TryRemove(new KeyValuePair<Guid, CacheEntry>(merchantId, entry));
            return true;
        }

        if (entry.IsStale && entry.RetryAt > now)
            return false;

        return now - entry.Snapshot.UpdatedAt >= SlidingTtl;
    }

    public void Set(IFoodMerchantSnapshot snapshot, bool isStale = false) =>
        _entries[snapshot.MerchantId] = new CacheEntry(
            snapshot,
            _timeProvider.GetUtcNow().Add(SlidingTtl),
            isStale,
            isStale ? _timeProvider.GetUtcNow().Add(StaleRetryDelay) : null);

    public void Remove(Guid merchantId) => _entries.TryRemove(merchantId, out _);

    public void AtualizarHorario(IFoodMerchantSnapshot snapshot) => Set(snapshot);

    private static IFoodMerchantContext ToContext(IFoodMerchantSnapshot snapshot, bool isStale) =>
        new(snapshot.DetailsJson, snapshot.StatusJson, snapshot.OpeningHoursJson, snapshot.UpdatedAt, isStale);

    private sealed record CacheEntry(
        IFoodMerchantSnapshot Snapshot,
        DateTimeOffset ExpiresAt,
        bool IsStale,
        DateTimeOffset? RetryAt);
}