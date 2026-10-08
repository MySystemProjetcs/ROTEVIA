using System.Net;
using DeliveryHub.Application.Abstractions;
using DeliveryHub.Application.Merchants;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Merchants;

internal sealed class ObterContextoIFood : IObterContextoIFood
{
    private static readonly TimeSpan SnapshotTtl = TimeSpan.FromMinutes(15);

    private readonly IMerchantRepository _merchants;
    private readonly IIFoodMerchantSnapshotStore _snapshots;
    private readonly IIFoodMerchantContextGateway _gateway;
    private readonly IFoodMerchantContextCache _cache;
    private readonly TimeProvider _timeProvider;

    public ObterContextoIFood(
        IMerchantRepository merchants,
        IIFoodMerchantSnapshotStore snapshots,
        IIFoodMerchantContextGateway gateway,
        IFoodMerchantContextCache cache,
        TimeProvider timeProvider)
    {
        _merchants = merchants;
        _snapshots = snapshots;
        _gateway = gateway;
        _cache = cache;
        _timeProvider = timeProvider;
    }

    public async Task<Result<IFoodMerchantContext>> ExecutarAsync(Guid merchantId, CancellationToken ct)
    {
        if (_cache.TryGet(merchantId, out var cached) && !_cache.RefreshIsDue(merchantId))
            return Result.Success(cached);

        var gate = _cache.LockFor(merchantId);
        await gate.WaitAsync(ct);
        try
        {
            if (_cache.TryGet(merchantId, out cached) && !_cache.RefreshIsDue(merchantId))
                return Result.Success(cached);

            var merchant = await _merchants.ObterPorIdAsync(merchantId, ct);
            if (merchant is null)
                return Result.Failure<IFoodMerchantContext>(IFoodMerchantContextErrors.MerchantNaoEncontrado);

            if (merchant.IFoodMerchantId is null || merchant.ConexaoIFood?.Conectado != true)
                return Result.Failure<IFoodMerchantContext>(IFoodMerchantContextErrors.ConexaoNaoAtiva);

            var snapshot = await _snapshots.ObterAsync(merchantId, ct);
            var now = _timeProvider.GetUtcNow();

            if (snapshot is not null && now - snapshot.UpdatedAt < SnapshotTtl)
            {
                _cache.Set(snapshot);
                return Result.Success(Contexto(snapshot, isStale: false));
            }

            try
            {
                var dados = await _gateway.ObterAsync(merchant, ct);
                snapshot = new IFoodMerchantSnapshot(
                    merchantId, dados.DetailsJson, dados.StatusJson, dados.OpeningHoursJson, now);
                await _snapshots.SalvarAsync(snapshot, ct);
                _cache.Set(snapshot);
                return Result.Success(Contexto(snapshot, isStale: false));
            }
            catch (IFoodMerchantAuthenticationException)
            {
                _cache.Remove(merchantId);
                return Result.Failure<IFoodMerchantContext>(ConexaoIFoodErrors.AutenticacaoIntegradaFalhou);
            }
            catch (HttpRequestException ex) when (
                ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                _cache.Remove(merchantId);
                return Result.Failure<IFoodMerchantContext>(ConexaoIFoodErrors.AutenticacaoIntegradaFalhou);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException)
            {
                if (snapshot is not null)
                {
                    _cache.Set(snapshot, isStale: true);
                    return Result.Success(Contexto(snapshot, isStale: true));
                }

                return Result.Failure<IFoodMerchantContext>(IFoodMerchantContextErrors.ProvedorIndisponivel);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private static IFoodMerchantContext Contexto(IFoodMerchantSnapshot snapshot, bool isStale) =>
        new(snapshot.DetailsJson, snapshot.StatusJson, snapshot.OpeningHoursJson, snapshot.UpdatedAt, isStale);
}