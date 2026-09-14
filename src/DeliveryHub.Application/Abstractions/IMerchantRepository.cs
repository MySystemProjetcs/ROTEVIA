using DeliveryHub.Domain.Merchants;

namespace DeliveryHub.Application.Abstractions;

public interface IMerchantRepository
{
    // excetoMerchantId é obrigatório de propósito: sem excluir o próprio
    // restaurante da checagem, uma loja nunca conseguiria reconfirmar ou migrar
    // sua própria conexão — bateria na trava contra si mesma.
    Task<bool> ExistePorIFoodIdAsync(Guid ifoodMerchantId, Guid excetoMerchantId, CancellationToken ct);
    Task<Merchant?> ObterPorIdAsync(Guid id, CancellationToken ct);

    // Só lojas conectadas pelo fluxo Distribuído — é sobre elas que o worker
    // precisa iterar, cada uma com seu próprio token.
    Task<IReadOnlyList<Merchant>> ListarConectadosAsync(CancellationToken ct);

    void Adicionar(Merchant merchant);
    Task SalvarAsync(CancellationToken ct);
}
