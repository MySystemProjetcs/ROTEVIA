namespace DeliveryHub.Application.Abstractions;

// Camada 2 do isolamento multi-tenant (CLAUDE.md §6). Resolvido do JWT e
// injetado — nunca ler claim direto no endpoint, porque aí o isolamento passa
// a depender de alguém lembrar.
public interface ITenantContext
{
    bool EstaAutenticado { get; }
    Guid? UsuarioId { get; }

    // Loja ativa. Nulo para o administrador do sistema e para o worker de
    // ingestão, que operam fora de uma loja específica.
    Guid? MerchantId { get; }

    // A única porta de saída do filtro por tenant. Vale para o administrador do
    // sistema e para processos internos (worker), nunca para lojista. Precisa
    // ser lida explicitamente: se algum caminho fizer isto virar true por
    // descuido, o isolamento entre clientes deixa de existir em silêncio.
    bool PodeVerTodosOsTenants { get; }
}
