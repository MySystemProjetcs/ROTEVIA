using DeliveryHub.Domain.Merchants;

namespace DeliveryHub.Application.Abstractions;

public interface IMerchantRepository
{
    Task<bool> ExistePorIFoodIdAsync(Guid ifoodMerchantId, CancellationToken ct);
    void Adicionar(Merchant merchant);
}
