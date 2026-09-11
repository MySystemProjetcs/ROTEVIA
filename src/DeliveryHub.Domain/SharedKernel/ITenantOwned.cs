namespace DeliveryHub.Domain.SharedKernel;

// Camada 1 do isolamento multi-tenant (ver CLAUDE.md §6): toda entidade que
// implementa esta interface precisa ter Global Query Filter registrado no
// EF Core por MerchantId. Teste de arquitetura garante isso quando o
// DbContext existir (Escopo 1, Passo 2).
public interface ITenantOwned
{
    Guid MerchantId { get; }
}
