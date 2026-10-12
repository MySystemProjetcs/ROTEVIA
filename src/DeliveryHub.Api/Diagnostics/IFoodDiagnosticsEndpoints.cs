using DeliveryHub.Infrastructure.Integrations.IFood.Auth;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DeliveryHub.Api.Diagnostics;

// Endpoint temporário de "Validação viva" (CLAUDE.md §2, Passo 3) só para a
// fatia de autenticação OAuth do Escopo 1. Nunca devolve o token em si.
public static class IFoodDiagnosticsEndpoints
{
    public static void MapIFoodDiagnosticsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/diagnostics/ifood-auth", CheckAuthentication);
    }

    private static async Task<Results<Ok<object>, ProblemHttpResult>> CheckAuthentication(
        IIFoodAuthenticator authenticator,
        CancellationToken ct)
    {
        try
        {
            var header = await authenticator.GetAuthorizationHeaderAsync(ct);
            return TypedResults.Ok<object>(new
            {
                authenticated = true,
                scheme = header.Scheme,
                tokenLength = header.Parameter?.Length ?? 0
            });
        }
        catch (IFoodAuthenticationException ex)
        {
            return TypedResults.Problem(
                title: "Falha ao autenticar na API do iFood",
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
