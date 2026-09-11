using System.Net.Http.Json;
using DeliveryHub.Infrastructure.Integrations.IFood.Contracts;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Orders;

internal interface IIFoodOrderClient
{
    // O evento do polling não diz de qual loja é o pedido — o merchant só
    // aparece aqui, e é por ele que o tenant é resolvido.
    Task<IFoodOrderDetails> GetDetailsAsync(Guid orderId, CancellationToken ct);
}

internal sealed class IFoodOrderClient : IIFoodOrderClient
{
    private readonly HttpClient _httpClient;

    public IFoodOrderClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IFoodOrderDetails> GetDetailsAsync(Guid orderId, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync($"orders/{orderId}", ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<IFoodErrorResponse>(ct);
            throw new IFoodApiException(
                response.StatusCode,
                error?.Error.Message ?? $"Falha ao buscar o pedido no iFood (HTTP {(int)response.StatusCode}).",
                error?.Error.Code);
        }

        return await response.Content.ReadFromJsonAsync<IFoodOrderDetails>(ct)
            ?? throw new IFoodApiException(response.StatusCode, "Detalhe do pedido veio vazio.");
    }
}
