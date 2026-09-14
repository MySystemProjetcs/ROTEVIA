using DeliveryHub.Domain.Identity;

namespace DeliveryHub.Api.Identity;

// Autorização por policy, nunca Roles = "STRING" solta no endpoint
// (ENGINEERING-GUIDE §2): o nome fica num lugar só e a regra pode crescer sem
// caçar string pelo código.
public static class Policies
{
    public const string AdministradorSistema = nameof(AdministradorSistema);
    public const string OperadorDaLoja = nameof(OperadorDaLoja);
    public const string Entregador = nameof(Entregador);
}

public static class AuthorizationSetup
{
    public static IServiceCollection AddPoliticasDeAutorizacao(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.AdministradorSistema, p =>
                p.RequireRole(nameof(PapelUsuario.AdministradorSistema)))
            .AddPolicy(Policies.OperadorDaLoja, p => p.RequireAssertion(contexto =>
                // Dono só entra com a loja ativa no token: sem ela o
                // ITenantContext não resolve tenant e a consulta não teria por
                // onde filtrar. O administrador entra sem loja porque opera
                // acima delas — quem limita o que ele vê é o filtro do
                // DbContext, não esta policy.
                contexto.User.IsInRole(nameof(PapelUsuario.AdministradorSistema)) ||
                (contexto.User.IsInRole(nameof(PapelUsuario.DonoRestaurante)) &&
                 contexto.User.HasClaim(c => c.Type == Infrastructure.Identity.ClaimsDeliveryHub.MerchantId))))
            // Sem merchant_id de propósito: o motoboy pode atender mais de um
            // restaurante, então não há uma única loja pra travar aqui — a
            // autorização por pedido específico acontece dentro do endpoint.
            .AddPolicy(Policies.Entregador, p => p.RequireRole(nameof(PapelUsuario.Entregador)));

        return services;
    }
}
