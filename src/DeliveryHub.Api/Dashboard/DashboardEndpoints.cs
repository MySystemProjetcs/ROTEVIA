using DeliveryHub.Application.Abstractions;
using DeliveryHub.Application.Dashboard;
using DeliveryHub.Application.Merchants;
using DeliveryHub.Domain.SharedKernel;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DeliveryHub.Api.Dashboard;

public sealed record ResumoDashboardResponse(
    int MotoboysOnline,
    int MotoboysEmEntrega,
    int QtdPedidosHoje,
    decimal ReceitaHoje,
    decimal TaxaPorEntrega);

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/restaurantes")
            .WithTags("Dashboard")
            .RequireAuthorization(Identity.Policies.OperadorDaLoja);

        group.MapGet("/{merchantId:guid}/dashboard/resumo", Resumo);
    }

    private static async Task<Results<Ok<ResumoDashboardResponse>, ProblemHttpResult>> Resumo(
        Guid merchantId,
        ITenantContext tenant,
        IObterResumoDashboard resumo,
        CancellationToken ct)
    {
        if (!PodeAcessar(tenant, merchantId))
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        var resultado = await resumo.ExecutarAsync(merchantId, ct);

        return TypedResults.Ok(new ResumoDashboardResponse(
            resultado.MotoboysOnline,
            resultado.MotoboysEmEntrega,
            resultado.QtdPedidosHoje,
            resultado.ReceitaHoje,
            resultado.TaxaPorEntrega));
    }

    // Mesma lógica dos demais endpoints de restaurante: 404 em vez de 403 —
    // não revela que a loja alheia existe.
    private static bool PodeAcessar(ITenantContext tenant, Guid merchantId) =>
        tenant.PodeVerTodosOsTenants || tenant.MerchantId == merchantId;

    private static ProblemHttpResult ProblemaDe(Error erro) => TypedResults.Problem(
        title: erro.Message,
        detail: erro.Code,
        statusCode: erro.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError
        });
}
