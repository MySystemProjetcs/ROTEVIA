using DeliveryHub.Application.Abstractions;
using DeliveryHub.Infrastructure.Integrations.IFood;
using DeliveryHub.Infrastructure.Integrations.IFood.Auth;
using DeliveryHub.Infrastructure.Integrations.IFood.Catalog;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DeliveryHub.Api.Diagnostics;

// Endpoints temporários de "Validação viva" (CLAUDE.md §2, Passo 3). Nunca
// devolvem token nem segredo.
public static class IFoodDiagnosticsEndpoints
{
    public static void MapIFoodDiagnosticsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/diagnostics/ifood-auth", CheckAuthentication);

        // Exige sessão: usa o merchant do token e bate numa API externa em
        // nome da loja. Sem isto, qualquer um na rede dispararia chamadas ao
        // iFood com as credenciais do app.
        app.MapGet("/diagnostics/ifood-catalog", CheckCatalogScope)
            .RequireAuthorization(Identity.Policies.OperadorDaLoja);
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

    // Responde a pergunta que bloqueia a implementação do módulo: o escopo de
    // catálogo está liberado para este clientId? O app do projeto nasceu com
    // Order, Events e Merchant (CLAUDE.md §7); Catalog precisa ser habilitado
    // no portal do iFood, e até lá a API recusa com 403 — não adianta
    // escrever os outros endpoints antes de saber disso.
    private static async Task<Results<Ok<object>, ProblemHttpResult>> CheckCatalogScope(
        ITenantContext tenant,
        IMerchantRepository merchants,
        IIFoodCatalogClient catalogo,
        CancellationToken ct)
    {
        if (tenant.MerchantId is not Guid merchantId)
            return TypedResults.Problem(
                title: "A sessão não está vinculada a um restaurante.",
                statusCode: StatusCodes.Status400BadRequest);

        var merchant = await merchants.ObterPorIdAsync(merchantId, ct);
        if (merchant?.IFoodMerchantId is not Guid ifoodMerchantId)
            return TypedResults.Problem(
                title: "Restaurante sem vínculo com o iFood.",
                detail: "Conecte a loja ao iFood antes de consultar o catálogo.",
                statusCode: StatusCodes.Status409Conflict);

        try
        {
            var catalogos = await catalogo.ListarCatalogosAsync(ifoodMerchantId, ct);
            return TypedResults.Ok<object>(new
            {
                escopoLiberado = true,
                quantidade = catalogos.Count,
                catalogos = catalogos.Select(c => new
                {
                    c.CatalogId,
                    c.Status,
                    c.Context,
                    c.ModifiedAt,
                }),
            });
        }
        catch (IFoodApiException ex)
        {
            // 403 aqui quase sempre significa módulo não habilitado, e não
            // credencial inválida — o mesmo token funciona em Order e Events.
            return TypedResults.Problem(
                title: ex.StatusCode == System.Net.HttpStatusCode.Forbidden
                    ? "Escopo de catálogo não liberado para este app no iFood."
                    : "Falha ao consultar o catálogo no iFood.",
                detail: ex.Message,
                statusCode: (int)ex.StatusCode);
        }
    }
}
