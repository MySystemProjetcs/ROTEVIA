using System.Net.Http.Json;
using System.Text.Json;
using DeliveryHub.Infrastructure.Integrations.IFood.Contracts;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Catalog;

// DTOs que cruzam a fronteira do assembly. Não são o contrato do iFood:
// aqueles (IFoodCatalog, IFoodSellableItem, ...) moram em Contracts e são
// internal por regra de arquitetura testada — nenhum tipo de lá pode ser
// público, senão o formato do marketplace vaza para quem consome. Estes aqui
// são nossos, e o client traduz.

public sealed record CatalogoDoIFood(
    string CatalogId,
    string? Status,
    IReadOnlyList<string> Context,
    DateTimeOffset? ModifiedAt,
    string? GroupId);

public sealed record ItemDoCatalogoIFood(
    string ItemId,
    string? CategoryId,
    string Nome,
    string? Descricao,
    decimal Preco,
    decimal? PrecoOriginal,
    bool Disponivel,
    IReadOnlyList<string> Restricoes,
    string? ImagemUrl,
    int Ordem);

public sealed record CategoriaDoCatalogoIFood(
    string CategoryId,
    string Nome,
    int Ordem,
    bool Pausada,
    IReadOnlyList<string> Restricoes,
    IReadOnlyList<ItemDoCatalogoIFood> Itens);

public sealed record CatalogoCompletoIFood(
    string GroupId,
    IReadOnlyList<CategoriaDoCatalogoIFood> Categorias,
    int TotalItens);

public interface IIFoodCatalogClient
{
    // Catálogos de uma loja. Um merchant costuma ter mais de um: o de
    // delivery (context DEFAULT) e o de consumo no local (INDOOR). Quem chama
    // decide qual interessa — aqui devolvemos todos.
    Task<IReadOnlyList<CatalogoDoIFood>> ListarCatalogosAsync(Guid merchantId, CancellationToken ct);

    // Catálogo completo de um groupId: categorias com os itens vendáveis,
    // marcando quais estão pausados ou com violação (dado vindo do endpoint
    // paralelo /unsellableItems). Uma chamada só porque o painel do catálogo
    // nunca mostra "vendáveis" sem a razão de por que o resto não aparece.
    Task<CatalogoCompletoIFood> ObterCatalogoCompletoAsync(
        Guid merchantId, string catalogId, string groupId, CancellationToken ct);
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

    public async Task<CatalogoCompletoIFood> ObterCatalogoCompletoAsync(
        Guid merchantId, string catalogId, string groupId, CancellationToken ct)
    {
        // As duas chamadas são independentes: itens vendáveis vêm por groupId
        // (/catalogs/{groupId}/sellableItems — a doc chama assim o path, mesmo
        // que o nome do segmento seja catalogId no path), e itens não-vendáveis
        // por catalogId (/catalogs/{catalogId}/unsellableItems). Rodam em
        // paralelo porque o painel precisa das duas pra decidir o que mostrar.
        var tarefaItens = ListarItensVendaveisAsync(merchantId, groupId, ct);
        var tarefaPausados = ListarItensNaoVendaveisAsync(merchantId, catalogId, ct);
        await Task.WhenAll(tarefaItens, tarefaPausados);

        var itens = await tarefaItens;
        var pausados = await tarefaPausados;

        // Mapeia item.id -> restrições, categoria.id -> (pausada, restrições).
        // O lookup fica fora dos selects pra não refazer a construção dentro
        // do loop (um cardápio grande tem centenas de itens).
        var restricoesPorItem = pausados.Categories
            .SelectMany(c => c.UnsellableItems.Select(i => new { i.Id, i.Restrictions }))
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.First().Restrictions);

        var restricoesPorCategoria = pausados.Categories
            .ToDictionary(c => c.Id, c => (
                Pausada: c.Restrictions.Any(r => r.Contains("PAUSED", StringComparison.OrdinalIgnoreCase)),
                Restricoes: c.Restrictions));

        // Agrupa por categoria — um item sem categoria é edge case documentado
        // (produto solto no catálogo) mas raro. Vai pra uma categoria sintética
        // "Sem categoria" pra não sumir da tela.
        var categorias = itens
            .GroupBy(i => new { Id = i.CategoryId ?? "sem-categoria", i.CategoryName, i.CategoryIndex })
            .OrderBy(g => g.Key.CategoryIndex)
            .Select(g =>
            {
                var categoriaInfo = restricoesPorCategoria.GetValueOrDefault(g.Key.Id,
                    (Pausada: false, Restricoes: (IReadOnlyList<string>)Array.Empty<string>()));

                return new CategoriaDoCatalogoIFood(
                    CategoryId: g.Key.Id,
                    Nome: string.IsNullOrWhiteSpace(g.Key.CategoryName) ? "Sem categoria" : g.Key.CategoryName,
                    Ordem: g.Key.CategoryIndex,
                    Pausada: categoriaInfo.Pausada,
                    Restricoes: categoriaInfo.Restricoes,
                    Itens: g
                        .OrderBy(i => i.ItemIndex)
                        .Select(i => new ItemDoCatalogoIFood(
                            ItemId: i.ItemId,
                            CategoryId: i.CategoryId,
                            Nome: i.ItemName ?? "(sem nome)",
                            Descricao: string.IsNullOrWhiteSpace(i.ItemDescription) ? null : i.ItemDescription,
                            Preco: i.ItemPrice?.Value ?? 0m,
                            PrecoOriginal: i.ItemPrice is { OriginalValue: > 0 } p
                                && p.OriginalValue != p.Value
                                    ? p.OriginalValue
                                    : null,
                            // Se o item veio em /sellableItems, está vendável.
                            // Se o id aparece em /unsellableItems também, o
                            // iFood está em estado transitório — tratamos como
                            // vendável porque é o que o /sellableItems disse,
                            // mas carregamos a restrição pra UI mostrar.
                            Disponivel: !restricoesPorItem.ContainsKey(i.ItemId),
                            Restricoes: restricoesPorItem.GetValueOrDefault(
                                i.ItemId, Array.Empty<string>()),
                            ImagemUrl: i.LogosUrls.FirstOrDefault(),
                            Ordem: i.ItemIndex))
                        .ToList());
            })
            .ToList();

        return new CatalogoCompletoIFood(groupId, categorias, itens.Count);
    }

    private async Task<IReadOnlyList<IFoodSellableItem>> ListarItensVendaveisAsync(
        Guid merchantId, string groupId, CancellationToken ct)
    {
        var escapado = Uri.EscapeDataString(groupId);
        using var response = await _httpClient.GetAsync(
            $"merchants/{merchantId}/catalogs/{escapado}/sellableItems", ct);

        if (!response.IsSuccessStatusCode)
            throw await ToExceptionAsync(response, "listar itens vendáveis", ct);

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<IFoodSellableItem>>(ct) ?? [];
    }

    private async Task<IFoodUnsellableResponse> ListarItensNaoVendaveisAsync(
        Guid merchantId, string catalogId, CancellationToken ct)
    {
        var escapado = Uri.EscapeDataString(catalogId);
        using var response = await _httpClient.GetAsync(
            $"merchants/{merchantId}/catalogs/{escapado}/unsellableItems", ct);

        // 404 aqui é benigno: significa que não há item pausado — não é motivo
        // pra derrubar a listagem inteira. 403 e demais erros caem no fluxo
        // normal, porque indicam permissão ou falha real.
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return new IFoodUnsellableResponse();

        if (!response.IsSuccessStatusCode)
            throw await ToExceptionAsync(response, "listar itens não-vendáveis", ct);

        return await response.Content.ReadFromJsonAsync<IFoodUnsellableResponse>(ct)
            ?? new IFoodUnsellableResponse();
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
