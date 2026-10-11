using DeliveryHub.Application.Merchants;
using DeliveryHub.Domain.SharedKernel;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DeliveryHub.Api.Merchants;

public static class ImpressoraEndpoints
{
    public static void MapImpressoraEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/restaurantes/{merchantId:guid}/impressoras")
            .WithTags("Impressoras")
            .RequireAuthorization();

        grupo.MapGet("/", Listar);
        grupo.MapPost("/", Cadastrar);
        grupo.MapPut("/{impressoraId:guid}", Atualizar);
        grupo.MapDelete("/{impressoraId:guid}", Remover);
    }

    private static async Task<Ok<IReadOnlyList<ImpressoraDto>>> Listar(
        Guid merchantId, IListarImpressoras caso, CancellationToken ct) =>
        TypedResults.Ok(await caso.ExecutarAsync(merchantId, ct));

    private static async Task<Results<Ok<ImpressoraDto>, ProblemHttpResult>> Cadastrar(
        Guid merchantId, ImpressoraInput input, ICadastrarImpressora caso, CancellationToken ct)
    {
        var resultado = await caso.ExecutarAsync(merchantId, input, ct);
        return resultado.IsSuccess ? TypedResults.Ok(resultado.Value) : ProblemaDe(resultado.Error);
    }

    private static async Task<Results<Ok<ImpressoraDto>, ProblemHttpResult>> Atualizar(
        Guid merchantId, Guid impressoraId, ImpressoraInput input,
        IAtualizarImpressora caso, CancellationToken ct)
    {
        var resultado = await caso.ExecutarAsync(merchantId, impressoraId, input, ct);
        return resultado.IsSuccess ? TypedResults.Ok(resultado.Value) : ProblemaDe(resultado.Error);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Remover(
        Guid merchantId, Guid impressoraId, IRemoverImpressora caso, CancellationToken ct)
    {
        var resultado = await caso.ExecutarAsync(merchantId, impressoraId, ct);
        return resultado.IsSuccess ? TypedResults.NoContent() : ProblemaDe(resultado.Error);
    }

    private static ProblemHttpResult ProblemaDe(Error erro) => TypedResults.Problem(
        title: erro.Message,
        detail: erro.Code,
        statusCode: erro.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError,
        });
}
