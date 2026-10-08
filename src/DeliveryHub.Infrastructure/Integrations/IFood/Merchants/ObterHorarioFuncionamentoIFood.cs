using System.Net;
using DeliveryHub.Application.Abstractions;
using DeliveryHub.Application.Merchants;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Merchants;

internal sealed class ObterHorarioFuncionamentoIFood : IObterHorarioFuncionamentoIFood
{
    private static readonly TimeSpan SnapshotTtl = TimeSpan.FromMinutes(15);

    private readonly IMerchantRepository _merchants;
    private readonly IIFoodOpeningHoursGateway _gateway;
    private readonly IIFoodMerchantSnapshotStore _snapshots;
    private readonly IFoodMerchantContextCache _cache;
    private readonly TimeProvider _timeProvider;

    public ObterHorarioFuncionamentoIFood(
        IMerchantRepository merchants,
        IIFoodOpeningHoursGateway gateway,
        IIFoodMerchantSnapshotStore snapshots,
        IFoodMerchantContextCache cache,
        TimeProvider timeProvider)
    {
        _merchants = merchants;
        _gateway = gateway;
        _snapshots = snapshots;
        _cache = cache;
        _timeProvider = timeProvider;
    }

    public async Task<Result<string>> ExecutarAsync(Guid merchantId, CancellationToken ct)
    {
        var gate = _cache.LockFor(merchantId);
        await gate.WaitAsync(ct);
        try
        {
            var merchant = await _merchants.ObterPorIdAsync(merchantId, ct);
            if (merchant is null)
                return Result.Failure<string>(IFoodMerchantContextErrors.MerchantNaoEncontrado);

            if (merchant.IFoodMerchantId is null || merchant.ConexaoIFood?.Conectado != true)
                return Result.Failure<string>(IFoodMerchantContextErrors.ConexaoNaoAtiva);

            var snapshot = await _snapshots.ObterAsync(merchantId, ct);
            var agora = _timeProvider.GetUtcNow();
            if (snapshot is not null && agora - snapshot.UpdatedAt < SnapshotTtl)
                return Result.Success(snapshot.OpeningHoursJson);

            try
            {
                var horariosJson = await _gateway.ObterHorarioAsync(merchant, ct);
                if (snapshot is not null)
                {
                    var atualizado = await _snapshots.AtualizarHorarioAsync(merchantId, horariosJson, agora, ct);
                    if (atualizado is not null) _cache.AtualizarHorario(atualizado);
                }

                return Result.Success(horariosJson);
            }
            catch (IFoodMerchantAuthenticationException)
            {
                _cache.Remove(merchantId);
                return Result.Failure<string>(ConexaoIFoodErrors.AutenticacaoIntegradaFalhou);
            }
            catch (HttpRequestException ex) when (
                ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                _cache.Remove(merchantId);
                return Result.Failure<string>(ConexaoIFoodErrors.AutenticacaoIntegradaFalhou);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException)
            {
                return snapshot is not null
                    ? Result.Success(snapshot.OpeningHoursJson)
                    : Result.Failure<string>(IFoodMerchantContextErrors.ProvedorIndisponivel);
            }
        }
        finally
        {
            gate.Release();
        }
    }
}