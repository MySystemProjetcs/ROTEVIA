using System.Net.Http.Headers;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Auth;

// Usado pelo worker de polling e por qualquer client iFood da Infrastructure
// para obter um Authorization válido, com cache e renovação transparentes.
public interface IIFoodAuthenticator
{
    Task<AuthenticationHeaderValue> GetAuthorizationHeaderAsync(CancellationToken ct);

    // Onboarding de merchant novo invalida o cache (CLAUDE.md §7 — "Token e
    // onboarding"): o token em cache não enxerga a loja recém-autorizada.
    // Do lado do iFood a permissão leva até 10 minutos para propagar.
    void InvalidateCache();
}
