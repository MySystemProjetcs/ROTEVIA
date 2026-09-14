using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace DeliveryHub.Infrastructure.Identity;

// Um lugar só sabe o nome das claims. O ITenantContext continua sendo a porta
// normal (CLAUDE.md §6), mas o Hub do SignalR não pode usá-lo: invocação de
// método de Hub não passa pelo pipeline HTTP, então IHttpContextAccessor vem
// nulo lá. O Hub tem o ClaimsPrincipal da conexão em Context.User, e lê por
// aqui em vez de conhecer o nome da claim por conta própria.
public static class ClaimsDoToken
{
    // O handler do JWT Bearer renomeia "sub" para ClaimTypes.NameIdentifier
    // antes de a claim chegar até nós (MapInboundClaims, ligado por padrão).
    public static Guid? UsuarioId(this ClaimsPrincipal? usuario) =>
        Analisar(
            usuario?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? usuario?.FindFirstValue(JwtRegisteredClaimNames.Sub));

    // Claim nossa: não sofre o mapeamento acima.
    public static Guid? MerchantId(this ClaimsPrincipal? usuario) =>
        Analisar(usuario?.FindFirstValue(ClaimsDeliveryHub.MerchantId));

    private static Guid? Analisar(string? valor) =>
        Guid.TryParse(valor, out var id) ? id : null;
}
