using System.Net.Http.Json;
using DeliveryHub.Infrastructure.Integrations.DiDiFood.Contracts;

namespace DeliveryHub.Infrastructure.Integrations.DiDiFood.Orders;

public interface IDiDiFoodOrderClient
{
    Task ConfirmOrderAsync(long orderId, string authToken, CancellationToken ct);
    Task OrderReadyAsync(long orderId, string authToken, CancellationToken ct);
    Task OrderDeliveredAsync(long orderId, string authToken, CancellationToken ct);
    Task CancelOrderAsync(long orderId, int reasonId, string authToken, CancellationToken ct);
}

internal sealed class DiDiFoodOrderClient : IDiDiFoodOrderClient
{
    private readonly HttpClient _http;

    public DiDiFoodOrderClient(HttpClient http)
    {
        _http = http;
    }

    public async Task ConfirmOrderAsync(long orderId, string authToken, CancellationToken ct)
    {
        var payload = new
        {
            auth_token = authToken,
            order_id = orderId
        };

        var response = await _http.PostAsJsonAsync("/v1/order/order/confirm", payload, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task OrderReadyAsync(long orderId, string authToken, CancellationToken ct)
    {
        var uri = $"/v1/order/order/ready?auth_token={Uri.EscapeDataString(authToken)}&order_id={orderId}";
        var response = await _http.GetAsync(uri, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task OrderDeliveredAsync(long orderId, string authToken, CancellationToken ct)
    {
        var uri = $"/v1/order/order/delivered?auth_token={Uri.EscapeDataString(authToken)}&order_id={orderId}";
        var response = await _http.GetAsync(uri, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task CancelOrderAsync(long orderId, int reasonId, string authToken, CancellationToken ct)
    {
        var payload = new
        {
            auth_token = authToken,
            order_id = orderId,
            reason_id = reasonId
        };

        var response = await _http.PostAsJsonAsync("/v1/order/order/cancel", payload, ct);
        response.EnsureSuccessStatusCode();
    }
}
