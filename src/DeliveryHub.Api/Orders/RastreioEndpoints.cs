using DeliveryHub.Application.Abstractions;
using DeliveryHub.Application.Tracking;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DeliveryHub.Api.Orders;

// Estado inicial do mapa. O SignalR não reenvia o que passou: quem recarrega a
// página entra no grupo e só recebe ping novo — sem isto o mapa nasce vazio e
// só se povoa quando algum motoboy emitir de novo.
public static class RastreioEndpoints
{
    public static void MapRastreioEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/rastreio/posicoes", Posicoes)
            .WithTags("Rastreio")
            .RequireAuthorization(Identity.Policies.OperadorDaLoja);
    }

    private static async Task<Results<Ok<IReadOnlyList<PosicaoRegistrada>>, ProblemHttpResult>> Posicoes(
        ITenantContext tenant,
        ICachePosicoes cache,
        CancellationToken ct)
    {
        // A loja vem do token, nunca da query: é o mesmo cuidado do hub — sem
        // isso uma loja leria a posição dos motoboys de outra.
        if (tenant.MerchantId is not { } merchantId)
            return TypedResults.Problem(
                title: "Sessão sem loja.",
                detail: "rastreio.sem_merchant",
                statusCode: StatusCodes.Status400BadRequest);

        return TypedResults.Ok(await cache.ObterDaLojaAsync(merchantId, ct));
    }
}
