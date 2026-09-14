using System.Net.Http.Headers;
using DeliveryHub.Domain.Merchants;

namespace DeliveryHub.Application.Abstractions;

// Devolve o Authorization da loja para chamadas de recurso do fluxo
// Distribuído, renovando via refresh_token quando necessário.
public interface IIFoodMerchantTokenProvider
{
    // O merchant precisa estar com ConexaoIFood.Conectado — quem chama garante
    // isso (normalmente veio de IMerchantRepository.ListarConectadosAsync).
    Task<AuthenticationHeaderValue> ObterAsync(Merchant merchant, CancellationToken ct);
}
