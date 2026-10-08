using System.Globalization;
using System.Net;
using DeliveryHub.Application.Abstractions;
using DeliveryHub.Application.Merchants;
using DeliveryHub.Domain.SharedKernel;
using Microsoft.Extensions.Logging;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Merchants;

internal sealed class CriarHorarioFuncionamentoIFood : IConfigurarHorarioFuncionamentoIFood
{
    private static readonly HashSet<string> DiasDaSemana = new(StringComparer.Ordinal)
    {
        "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY", "SUNDAY"
    };

    private readonly IMerchantRepository _merchants;
    private readonly IIFoodOpeningHoursGateway _gateway;
    private readonly IIFoodMerchantSnapshotStore _snapshots;
    private readonly IFoodMerchantContextCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CriarHorarioFuncionamentoIFood> _logger;

    public CriarHorarioFuncionamentoIFood(
        IMerchantRepository merchants,
        IIFoodOpeningHoursGateway gateway,
        IIFoodMerchantSnapshotStore snapshots,
        IFoodMerchantContextCache cache,
        TimeProvider timeProvider,
        ILogger<CriarHorarioFuncionamentoIFood> logger)
    {
        _merchants = merchants;
        _gateway = gateway;
        _snapshots = snapshots;
        _cache = cache;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<string>> ExecutarAsync(Guid merchantId, IFoodOpeningHoursInput input, CancellationToken ct)
    {
        if (!HorarioValido(input))
            return Result.Failure<string>(IFoodMerchantContextErrors.HorarioFuncionamentoInvalido);

        var gate = _cache.LockFor(merchantId);
        await gate.WaitAsync(ct);
        try
        {
            var merchant = await _merchants.ObterPorIdAsync(merchantId, ct);
            if (merchant is null)
                return Result.Failure<string>(IFoodMerchantContextErrors.MerchantNaoEncontrado);

            if (merchant.IFoodMerchantId is null || merchant.ConexaoIFood?.Conectado != true)
                return Result.Failure<string>(IFoodMerchantContextErrors.ConexaoNaoAtiva);

            try
            {
                var responseJson = await _gateway.CriarAsync(merchant, input, ct);
                var agora = _timeProvider.GetUtcNow();
                var snapshot = await _snapshots.AtualizarHorarioAsync(merchantId, responseJson, agora, ct);

                if (snapshot is null)
                    _cache.Remove(merchantId);
                else
                    _cache.AtualizarHorario(snapshot);

                return Result.Success(responseJson);
            }
            catch (IFoodMerchantAuthenticationException ex)
            {
                _cache.Remove(merchantId);
                _logger.LogWarning(ex, "Autenticação da loja {MerchantId} falhou ao atualizar horários no iFood.", merchantId);
                return Result.Failure<string>(ConexaoIFoodErrors.AutenticacaoIntegradaFalhou);
            }
            catch (HttpRequestException ex) when (
                ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                _cache.Remove(merchantId);
                _logger.LogWarning(ex, "iFood recusou a atualização de horários da loja {MerchantId} com status {StatusCode}.", merchantId, ex.StatusCode);
                return ex.StatusCode == HttpStatusCode.BadRequest
                    ? Result.Failure<string>(IFoodMerchantContextErrors.HorarioFuncionamentoInvalido)
                    : Result.Failure<string>(ConexaoIFoodErrors.AutenticacaoIntegradaFalhou);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Falha de rede ao atualizar horários da loja {MerchantId} no iFood.", merchantId);
                return Result.Failure<string>(IFoodMerchantContextErrors.ProvedorIndisponivel);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private static bool HorarioValido(IFoodOpeningHoursInput? input) =>
        input?.Shifts is not null && input.Shifts.All(turno =>
            turno is not null &&
            DiasDaSemana.Contains(turno.DayOfWeek) &&
            TimeOnly.TryParseExact(turno.Start, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) &&
            turno.Duration is > 0 and <= 1440);
}