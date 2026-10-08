using DeliveryHub.Domain.Merchants;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Application.Abstractions;

public sealed record IFoodMerchantData(string DetailsJson, string StatusJson, string OpeningHoursJson);

public sealed record IFoodMerchantSnapshot(
    Guid MerchantId,
    string DetailsJson,
    string StatusJson,
    string OpeningHoursJson,
    DateTimeOffset UpdatedAt);

public sealed record IFoodMerchantContext(
    string DetailsJson,
    string StatusJson,
    string OpeningHoursJson,
    DateTimeOffset UpdatedAt,
    bool IsStale);

public sealed record IFoodOpeningHourShift(string DayOfWeek, string Start, int Duration);

public sealed record IFoodOpeningHoursInput(IReadOnlyList<IFoodOpeningHourShift> Shifts);

public interface IIFoodMerchantContextGateway
{
    Task<IFoodMerchantData> ObterAsync(Merchant merchant, CancellationToken ct);
}

public interface IIFoodOpeningHoursGateway
{
    Task<string> ObterHorarioAsync(Merchant merchant, CancellationToken ct);
    Task<string> CriarAsync(Merchant merchant, IFoodOpeningHoursInput input, CancellationToken ct);
}

public interface IIFoodMerchantSnapshotStore
{
    Task<IFoodMerchantSnapshot?> ObterAsync(Guid merchantId, CancellationToken ct);
    Task SalvarAsync(IFoodMerchantSnapshot snapshot, CancellationToken ct);
    Task<IFoodMerchantSnapshot?> AtualizarHorarioAsync(
        Guid merchantId, string openingHoursJson, DateTimeOffset updatedAt, CancellationToken ct);
}

public interface IObterContextoIFood
{
    Task<Result<IFoodMerchantContext>> ExecutarAsync(Guid merchantId, CancellationToken ct);
}

public interface IConfigurarHorarioFuncionamentoIFood
{
    Task<Result<string>> ExecutarAsync(Guid merchantId, IFoodOpeningHoursInput input, CancellationToken ct);
}

public interface IObterHorarioFuncionamentoIFood
{
    Task<Result<string>> ExecutarAsync(Guid merchantId, CancellationToken ct);
}

public static class IFoodMerchantContextErrors
{
    public static readonly Error AutenticacaoIntegradaFalhou =
        DeliveryHub.Application.Merchants.ConexaoIFoodErrors.AutenticacaoIntegradaFalhou;

    public static readonly Error MerchantNaoEncontrado = new(
        "ifood.merchant_nao_encontrado", "Restaurante não encontrado.", ErrorType.NotFound);

    public static readonly Error ConexaoNaoAtiva = new(
        "ifood.conexao_nao_ativa", "Restaurante não possui conexão ativa com o iFood.", ErrorType.Conflict);

    public static readonly Error ProvedorIndisponivel = new(
        "ifood.provedor_indisponivel", "Não foi possível consultar os dados do iFood.", ErrorType.Failure);

    public static readonly Error HorarioFuncionamentoInvalido = new(
        "ifood.horario_funcionamento_invalido",
        "Os horários foram recusados pelo iFood. Verifique turnos sobrepostos no mesmo dia e use intervalos em múltiplos de 30 minutos; o detalhe da recusa está no log do servidor.",
        ErrorType.Validation);

}