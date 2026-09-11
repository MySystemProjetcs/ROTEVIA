using System.Net;
using System.Net.Http.Json;
using DeliveryHub.Infrastructure.Integrations.IFood.Contracts;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Polling;

internal interface IIFoodEventsClient
{
    Task<IReadOnlyList<IFoodEvent>> PollAsync(CancellationToken ct);

    // Só chamar depois de persistir: acknowledgment antes de gravar perde
    // pedido se o processo cair no meio (CLAUDE.md §7).
    Task AcknowledgeAsync(IReadOnlyList<Guid> eventIds, CancellationToken ct);
}

internal sealed class IFoodEventsClient : IIFoodEventsClient
{
    // O caminho começa com "events:" e um Uri relativo assim seria lido como
    // esquema. O "./" à frente força interpretação como caminho.
    private const string PollingPath = "./events:polling";
    private const string AcknowledgmentPath = "events/acknowledgment";

    private readonly HttpClient _httpClient;

    public IFoodEventsClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<IFoodEvent>> PollAsync(CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(PollingPath, ct);

        // Sem evento novo o iFood responde 204 com corpo vazio — desserializar
        // isso estoura, então o caso precisa sair antes.
        if (response.StatusCode == HttpStatusCode.NoContent)
            return [];

        if (!response.IsSuccessStatusCode)
            throw await ToExceptionAsync(response, ct);

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<IFoodEvent>>(ct) ?? [];
    }

    public async Task AcknowledgeAsync(IReadOnlyList<Guid> eventIds, CancellationToken ct)
    {
        if (eventIds.Count == 0)
            return;

        var payload = eventIds.Select(id => new IFoodAckEvent(id)).ToArray();

        using var response = await _httpClient.PostAsJsonAsync(AcknowledgmentPath, payload, ct);

        if (!response.IsSuccessStatusCode)
            throw await ToExceptionAsync(response, ct);
    }

    private static async Task<IFoodApiException> ToExceptionAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadFromJsonAsync<IFoodPollingErrorResponse>(ct);

        return new IFoodApiException(
            response.StatusCode,
            body?.Error.Message ?? $"Falha na API de eventos do iFood (HTTP {(int)response.StatusCode}).",
            body?.Error.Code,
            body?.Error.UnauthorizedMerchants);
    }
}
