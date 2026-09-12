using DeliveryHub.Application.Abstractions;

namespace DeliveryHub.Infrastructure.Persistence;

// Contexto dos processos internos sem requisição HTTP — hoje, o worker de
// ingestão. Ele opera entre tenants por natureza: o iFood entrega os eventos de
// todas as lojas num fluxo só e é o nosso código que separa, resolvendo o
// merchant evento a evento.
// Registrado explicitamente no Program.cs do worker e em lugar nenhum mais: se
// a API passar a usar este contexto, o isolamento entre clientes acaba.
public sealed class TenantContextSistema : ITenantContext
{
    public bool EstaAutenticado => false;
    public Guid? UsuarioId => null;
    public Guid? MerchantId => null;
    public bool PodeVerTodosOsTenants => true;
}
