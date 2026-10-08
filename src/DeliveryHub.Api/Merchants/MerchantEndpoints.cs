using System.Text.Json;
using DeliveryHub.Application.Abstractions;
using DeliveryHub.Application.Merchants;
using DeliveryHub.Domain.SharedKernel;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DeliveryHub.Api.Merchants;

public sealed record IniciarConexaoResponse(string UserCode, string VerificationUrlComplete, DateTimeOffset ExpiraEm);
public sealed record ConfirmarConexaoRequest(string AuthorizationCode);
public sealed record DefinirTaxaRequest(decimal Valor);
public sealed record DefinirNomeRequest(string Nome);
public sealed record TaxaEntregaResponse(decimal Valor);
public sealed record ConfigurarHorarioFuncionamentoRequest(IReadOnlyList<IFoodOpeningHourShift> Shifts);
public sealed record ContextoIFoodResponse(
    JsonElement Comerciante,
    JsonElement Status,
    JsonElement HorarioFuncionamento,
    DateTimeOffset AtualizadoEm,
    bool Desatualizado);

// Latitude/longitude opcionais: só são necessárias quando o geocoder não
// encontra o logradouro, o que acontece de verdade em rua fora do OpenStreetMap.
public sealed record EnderecoDaLojaRequest(
    string Cep,
    string Numero,
    string? Complemento,
    string? Referencia,
    double? Latitude,
    double? Longitude);

public static class MerchantEndpoints
{
    public static void MapMerchantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/restaurantes")
            .WithTags("Restaurantes")
            .RequireAuthorization(Identity.Policies.OperadorDaLoja);

        group.MapPost("/{merchantId:guid}/ifood/iniciar-conexao", IniciarConexao);
        group.MapPost("/{merchantId:guid}/ifood/confirmar", ConfirmarConexao);
        group.MapGet("/ifood/contexto", ObterContextoIFood);
        group.MapGet("/ifood/horario-funcionamento", ObterHorarioFuncionamentoIFood);
        group.MapPut("/ifood/horario-funcionamento", ConfigurarHorarioFuncionamentoIFood);
        group.MapPut("/{merchantId:guid}/nome", DefinirNome);
        group.MapPut("/{merchantId:guid}/taxa-entrega", DefinirTaxa);
        group.MapGet("/{merchantId:guid}/endereco", ObterEndereco);
        group.MapPut("/{merchantId:guid}/endereco", DefinirEndereco);
    }

    private static async Task<Results<Ok<ContextoIFoodResponse>, ProblemHttpResult>> ObterContextoIFood(
        ITenantContext tenant,
        IObterContextoIFood obter,
        CancellationToken ct)
    {
        if (tenant.MerchantId is not Guid merchantId)
            return TypedResults.Problem(
                title: "A sessão não está vinculada a um restaurante.",
                detail: "ifood.merchant_contexto_indisponivel",
                statusCode: StatusCodes.Status400BadRequest);

        var resultado = await obter.ExecutarAsync(merchantId, ct);
        if (resultado.IsFailure)
        {
            var statusCode = resultado.Error.Type switch
            {
                _ when resultado.Error.Code == ConexaoIFoodErrors.AutenticacaoIntegradaFalhou.Code =>
                    StatusCodes.Status502BadGateway,
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorType.Failure => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status500InternalServerError
            };

            return TypedResults.Problem(
                title: resultado.Error.Message,
                detail: resultado.Error.Code,
                statusCode: statusCode);
        }

        var contexto = resultado.Value;
        return TypedResults.Ok(new ContextoIFoodResponse(
            JsonDocument.Parse(contexto.DetailsJson).RootElement.Clone(),
            JsonDocument.Parse(contexto.StatusJson).RootElement.Clone(),
            JsonDocument.Parse(contexto.OpeningHoursJson).RootElement.Clone(),
            contexto.UpdatedAt,
            contexto.IsStale));
    }

    private static async Task<Results<Ok<JsonElement>, NotFound, ProblemHttpResult>> ObterHorarioFuncionamentoIFood(
        ITenantContext tenant,
        IObterHorarioFuncionamentoIFood obter,
        CancellationToken ct)
    {
        if (tenant.MerchantId is not Guid merchantId)
            return TypedResults.Problem(
                title: "A sessão não está vinculada a um restaurante.",
                detail: "ifood.merchant_contexto_indisponivel",
                statusCode: StatusCodes.Status400BadRequest);

        var resultado = await obter.ExecutarAsync(merchantId, ct);
        if (resultado.IsFailure)
            return ProblemaDe(resultado.Error);

        using var documento = JsonDocument.Parse(resultado.Value);
        var horarios = documento.RootElement.Clone();
        if (horarios.ValueKind == JsonValueKind.Array && horarios.GetArrayLength() == 0)
            return TypedResults.NotFound();

        return TypedResults.Ok(horarios);
    }

    private static async Task<Results<Created<JsonElement>, ProblemHttpResult>> ConfigurarHorarioFuncionamentoIFood(
        ConfigurarHorarioFuncionamentoRequest request,
        ITenantContext tenant,
        IConfigurarHorarioFuncionamentoIFood configurar,
        CancellationToken ct)
    {
        if (tenant.MerchantId is not Guid merchantId)
            return TypedResults.Problem(
                title: "A sessão não está vinculada a um restaurante.",
                detail: "ifood.merchant_contexto_indisponivel",
                statusCode: StatusCodes.Status400BadRequest);

        var resultado = await configurar.ExecutarAsync(
            merchantId,
            new IFoodOpeningHoursInput(request.Shifts),
            ct);

        if (resultado.IsFailure)
            return ProblemaDe(resultado.Error);

        using var documento = JsonDocument.Parse(resultado.Value);
        return TypedResults.Created((string?)null, documento.RootElement.Clone());
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DefinirNome(
        Guid merchantId,
        DefinirNomeRequest request,
        ITenantContext tenant,
        IDefinirNomeDaLoja definir,
        CancellationToken ct)
    {
        if (!PodeAcessar(tenant, merchantId))
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        var resultado = await definir.ExecutarAsync(merchantId, request.Nome, ct);

        return resultado.IsSuccess ? TypedResults.NoContent() : ProblemaDe(resultado.Error);
    }

    private static async Task<Results<Ok<EnderecoDaLojaDto>, NotFound, ProblemHttpResult>> ObterEndereco(
        Guid merchantId,
        ITenantContext tenant,
        IObterEnderecoDaLoja obter,
        CancellationToken ct)
    {
        if (!PodeAcessar(tenant, merchantId))
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        var endereco = await obter.ExecutarAsync(merchantId, ct);

        return endereco is null ? TypedResults.NotFound() : TypedResults.Ok(endereco);
    }

    private static async Task<Results<Ok<EnderecoDaLojaDto>, ProblemHttpResult>> DefinirEndereco(
        Guid merchantId,
        EnderecoDaLojaRequest request,
        ITenantContext tenant,
        IDefinirEnderecoDaLoja definir,
        CancellationToken ct)
    {
        if (!PodeAcessar(tenant, merchantId))
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        var resultado = await definir.ExecutarAsync(
            merchantId,
            new NovoEnderecoDaLoja(
                request.Cep,
                request.Numero,
                request.Complemento,
                request.Referencia,
                request.Latitude,
                request.Longitude),
            ct);

        return resultado.IsSuccess ? TypedResults.Ok(resultado.Value) : ProblemaDe(resultado.Error);
    }

    private static async Task<Results<Ok<IniciarConexaoResponse>, ProblemHttpResult>> IniciarConexao(
        Guid merchantId,
        ITenantContext tenant,
        IIniciarConexaoIFood iniciar,
        CancellationToken ct)
    {
        if (!PodeAcessar(tenant, merchantId))
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        var resultado = await iniciar.ExecutarAsync(merchantId, ct);

        return resultado.IsSuccess
            ? TypedResults.Ok(new IniciarConexaoResponse(
                resultado.Value.UserCode, resultado.Value.VerificationUrlComplete, resultado.Value.ExpiraEm))
            : ProblemaDe(resultado.Error);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ConfirmarConexao(
        Guid merchantId,
        ConfirmarConexaoRequest request,
        ITenantContext tenant,
        IConfirmarConexaoIFood confirmar,
        CancellationToken ct)
    {
        if (!PodeAcessar(tenant, merchantId))
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        var resultado = await confirmar.ExecutarAsync(merchantId, request.AuthorizationCode, ct);

        return resultado.IsSuccess ? TypedResults.NoContent() : ProblemaDe(resultado.Error);
    }

    private static async Task<Results<Ok<TaxaEntregaResponse>, ProblemHttpResult>> DefinirTaxa(
        Guid merchantId,
        DefinirTaxaRequest request,
        ITenantContext tenant,
        IDefinirTaxaPorEntrega definir,
        CancellationToken ct)
    {
        if (!PodeAcessar(tenant, merchantId))
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        var resultado = await definir.ExecutarAsync(merchantId, request.Valor, ct);

        return resultado.IsSuccess
            ? TypedResults.Ok(new TaxaEntregaResponse(resultado.Value))
            : ProblemaDe(resultado.Error);
    }

    // A policy só garante que existe ALGUM merchant_id no token; sem isto, o
    // dono da Loja A poderia iniciar/confirmar a conexão da Loja B só trocando
    // o GUID na URL. 404 em vez de 403 — não revela que a loja alheia existe.
    private static bool PodeAcessar(ITenantContext tenant, Guid merchantId) =>
        tenant.PodeVerTodosOsTenants || tenant.MerchantId == merchantId;

    private static ProblemHttpResult ProblemaDe(Error erro) => TypedResults.Problem(
        title: erro.Message,
        detail: erro.Code,
        statusCode: erro.Code == ConexaoIFoodErrors.AutenticacaoIntegradaFalhou.Code
            ? StatusCodes.Status502BadGateway
            : erro.Type switch
            {
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorType.Failure => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status500InternalServerError
            });
}
