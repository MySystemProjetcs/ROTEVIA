using System.Net.Http.Json;
using System.Text.Json;
using DeliveryHub.Infrastructure.Integrations.IFood.Contracts;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Catalog;

internal interface IIFoodCatalogClient
{
    // Catálogos de uma loja. Um merchant costuma ter mais de um: o de
    // delivery (context DEFAULT) e o de consumo no local (INDOOR). Quem chama
    // decide qual interessa — aqui devolvemos todos.
    Task<IReadOnlyList<IFoodCatalog>> ListarCatalogosAsync(Guid merchantId, CancellationToken ct);
}

internal sealed class IFoodCatalogClient : IIFoodCatalogClient
{
    private readonly HttpClient _httpClient;

    public IFoodCatalogClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<IFoodCatalog>> ListarCatalogosAsync(Guid merchantId, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync($"merchants/{merchantId}/catalogs", ct);

        if (!response.IsSuccessStatusCode)
            throw await ToExceptionAsync(response, "listar catálogos", ct);

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<IFoodCatalog>>(ct) ?? [];
    }

    // O corpo de erro do Catalog é plano (code/message/requestId/details), não
    // o { "error": { ... } } do resto da API — ver IFoodCatalogContracts.
    // Quando nem esse formato vem (gateway caindo antes de chegar no módulo,
    // HTML de proxy), cai na mensagem genérica em vez de estourar.
    private static async Task<IFoodApiException> ToExceptionAsync(
        HttpResponseMessage response, string operacao, CancellationToken ct)
    {
        IFoodCatalogErrorResponse? corpo = null;
        try
        {
            corpo = await response.Content.ReadFromJsonAsync<IFoodCatalogErrorResponse>(ct);
        }
        catch (JsonException)
        {
            // corpo fica nulo: a mensagem genérica abaixo ainda diz o status.
        }

        var detalhe = corpo?.Resumir()
            ?? $"Falha ao {operacao} no iFood (HTTP {(int)response.StatusCode}).";

        return new IFoodApiException(response.StatusCode, detalhe, corpo?.Code);
    }
}
