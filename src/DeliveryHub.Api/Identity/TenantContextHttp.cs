using System.Security.Claims;
using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Identity;
using DeliveryHub.Infrastructure.Identity;

namespace DeliveryHub.Api.Identity;

// Camada 2 do isolamento (CLAUDE.md §6): o tenant sai do token e é injetado.
// Tudo aqui é somente leitura e falha fechado — na dúvida, sem acesso.
internal sealed class TenantContextHttp : ITenantContext
{
    private readonly ClaimsPrincipal? _usuario;

    public TenantContextHttp(IHttpContextAccessor accessor)
    {
        _usuario = accessor.HttpContext?.User;
    }

    public bool EstaAutenticado => _usuario?.Identity?.IsAuthenticated == true;

    public Guid? UsuarioId => _usuario.UsuarioId();

    public Guid? MerchantId => _usuario.MerchantId();

    // Só o papel de administrador abre o filtro, e só quando o token é válido.
    // Requisição sem autenticação não vê nada de ninguém.
    public bool PodeVerTodosOsTenants =>
        EstaAutenticado && _usuario!.IsInRole(nameof(PapelUsuario.AdministradorSistema));
}
