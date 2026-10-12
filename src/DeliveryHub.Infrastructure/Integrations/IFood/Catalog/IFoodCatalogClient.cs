using System.Net.Http.Json;
using System.Text.Json;
using DeliveryHub.Infrastructure.Integrations.IFood.Contracts;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Catalog;

// DTO que cruza a fronteira do assembly. Não é o contrato do iFood: aquele
// (IFoodCatalog) mora em Contracts e é internal por regra de arquitetura
// testada — nenhum tipo de lá pode ser público, senão o formato do
// marketplace vaza para quem consome. Este aqui é nosso, e o client traduz.
public sealed record CatalogoDoIFood(
    string CatalogId,
    string? Status,
    IReadOnlyList<string> Context,
    DateTimeOffset? ModifiedAt,
    string? GroupId);

public interface IIFoodCatalogClient
{
    // Catálogos de uma loja. Um merchant costuma ter mais de um: o de
    // delivery (context DEFAULT) e o de consumo no local (INDOOR). Quem chama
    // decide qual interessa — aqui devolvemos todos.
    Task<IReadOnlyList<CatalogoDoIFood>> ListarCatalogosAsync(Guid merchantId, CancellationToken ct);
}

internal sealed class IFoodCatalogClient : IIFoodCatalogClient
{
    private readonly HttpClient _httpClient;

    public IFoodCatalogClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<CatalogoDoIFood>> ListarCatalogosAsync(
        Guid merchantId, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync($"merchants/{merchantId}/catalogs", ct);

        if (!response.IsSuccessStatusCode)
            throw await ToExceptionAsync(response, "listar catálogos", ct);

        var corpo = await response.Content.ReadFromJsonAsync<IReadOnlyList<IFoodCatalog>>(ct) ?? [];

        return corpo
            .Select(c => new CatalogoDoIFood(c.CatalogId, c.Status, c.Context, c.ModifiedAt, c.GroupId))
            .ToList();
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
