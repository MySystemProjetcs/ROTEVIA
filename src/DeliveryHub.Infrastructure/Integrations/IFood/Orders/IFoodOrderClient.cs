using System.Net.Http.Headers;
using System.Net.Http.Json;
using DeliveryHub.Infrastructure.Integrations.IFood.Contracts;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Orders;

internal interface IIFoodOrderClient
{
    // O evento do polling não diz de qual loja é o pedido — o merchant só
    // aparece aqui, e é por ele que o tenant é resolvido. autorizacao nulo usa
    // o token Centralizado padrão; uma loja do Distribuído passa o próprio.
    Task<IFoodOrderDetails> GetDetailsAsync(Guid orderId, CancellationToken ct, AuthenticationHeaderValue? autorizacao = null);

    Task ConfirmAsync(Guid orderId, CancellationToken ct);
    Task StartPreparationAsync(Guid orderId, CancellationToken ct);
    Task ReadyToPickupAsync(Guid orderId, CancellationToken ct);
    Task DispatchAsync(Guid orderId, CancellationToken ct);
}

internal sealed class IFoodOrderClient : IIFoodOrderClient
{
    private readonly HttpClient _httpClient;

    public IFoodOrderClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IFoodOrderDetails> GetDetailsAsync(
        Guid orderId, CancellationToken ct, AuthenticationHeaderValue? autorizacao = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"orders/{orderId}");
        if (autorizacao is not null)
            request.Headers.Authorization = autorizacao;

        using var response = await _httpClient.SendAsync(request, ct);

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

    // Todas as ações são POST sem corpo e respondem 202 Accepted; a mudança de
    // status chega depois como evento do polling.
    public Task ConfirmAsync(Guid orderId, CancellationToken ct) =>
        AcionarAsync(orderId, "confirm", ct);

    public Task StartPreparationAsync(Guid orderId, CancellationToken ct) =>
        AcionarAsync(orderId, "startPreparation", ct);

    public Task ReadyToPickupAsync(Guid orderId, CancellationToken ct) =>
        AcionarAsync(orderId, "readyToPickup", ct);

    public Task DispatchAsync(Guid orderId, CancellationToken ct) =>
        AcionarAsync(orderId, "dispatch", ct);

    private async Task AcionarAsync(Guid orderId, string acao, CancellationToken ct)
    {
        using var response = await _httpClient.PostAsync($"orders/{orderId}/{acao}", content: null, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<IFoodErrorResponse>(ct);
            throw new IFoodApiException(
                response.StatusCode,
                error?.Error.Message ?? $"O iFood recusou a ação '{acao}' (HTTP {(int)response.StatusCode}).",
                error?.Error.Code);
        }
    }
}
