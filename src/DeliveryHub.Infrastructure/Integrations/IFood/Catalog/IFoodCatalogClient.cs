using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeliveryHub.Infrastructure.Integrations.IFood.Contracts;
using Microsoft.Extensions.Logging;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Catalog;

// DTOs que cruzam a fronteira do assembly. Não são o contrato do iFood:
// aquele mora em Contracts e é internal por regra de arquitetura testada —
// nenhum tipo de lá pode ser público, senão o formato do marketplace vaza
// para quem consome. Estes aqui são nossos, e o client traduz.

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

    // Itens vendáveis do catálogo agrupados por categoria, marcando o que o
    // iFood aponta como pausado ou com violação.
    Task<CatalogoCompletoIFood> ObterCatalogoCompletoAsync(
        Guid merchantId, string catalogId, string groupId, CancellationToken ct);
}

internal sealed class IFoodCatalogClient : IIFoodCatalogClient
{
    private const string SemCategoria = "sem-categoria";
    private const int TamanhoDoTrechoNoLog = 500;

    private readonly HttpClient _httpClient;
    private readonly ILogger<IFoodCatalogClient> _logger;

    public IFoodCatalogClient(HttpClient httpClient, ILogger<IFoodCatalogClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    // GET /merchants/{merchantId}/catalogs. O merchantId aqui é o id da loja
    // no iFood, não o id interno do restaurante.
    public async Task<IReadOnlyList<CatalogoDoIFood>> ListarCatalogosAsync(
        Guid merchantId, CancellationToken ct)
    {
        const string operacao = "listar catálogos";

        using var documento = await GetJsonAsync($"merchants/{merchantId}/catalogs", operacao, ct);

        return ListaDaRaiz(documento.RootElement, operacao)
            .Select(LerCatalogo)
            .OfType<CatalogoDoIFood>()
            .ToList();
    }

    public async Task<CatalogoCompletoIFood> ObterCatalogoCompletoAsync(
        Guid merchantId, string catalogId, string groupId, CancellationToken ct)
    {
        // Vendáveis vêm por groupId, não-vendáveis por catalogId — é o que a
        // doc do Catalog v2.0 pede em cada rota. Independentes, então em
        // paralelo.
        var tarefaItens = ListarItensVendaveisAsync(merchantId, groupId, ct);
        var tarefaRestricoes = LerRestricoesAsync(merchantId, catalogId, ct);
        await Task.WhenAll(tarefaItens, tarefaRestricoes);

        var itens = await tarefaItens;
        var restricoes = await tarefaRestricoes;

        var categorias = itens
            .GroupBy(i => i.CategoryId ?? SemCategoria)
            .Select(g =>
            {
                var daCategoria = restricoes.PorCategoria.GetValueOrDefault(g.Key) ?? Array.Empty<string>();

                return new CategoriaDoCatalogoIFood(
                    CategoryId: g.Key,
                    Nome: g.Select(i => i.CategoryName).FirstOrDefault(n => n is not null) ?? "Sem categoria",
                    Ordem: g.Min(i => i.CategoryIndex),
                    Pausada: daCategoria.Any(r => r.Contains("PAUSED", StringComparison.OrdinalIgnoreCase)),
                    Restricoes: daCategoria,
                    Itens: g
                        .OrderBy(i => i.ItemIndex)
                        .ThenBy(i => i.Nome, StringComparer.CurrentCulture)
                        .Select(i =>
                        {
                            var doItem = restricoes.PorItem.GetValueOrDefault(i.ItemId);
                            return new ItemDoCatalogoIFood(
                                ItemId: i.ItemId,
                                CategoryId: i.CategoryId,
                                Nome: i.Nome,
                                Descricao: i.Descricao,
                                Preco: i.Preco,
                                PrecoOriginal: i.PrecoOriginal,
                                Disponivel: doItem is null,
                                Restricoes: doItem ?? Array.Empty<string>(),
                                ImagemUrl: i.ImagemUrl,
                                Ordem: i.ItemIndex);
                        })
                        .ToList());
            })
            .OrderBy(c => c.Ordem)
            .ThenBy(c => c.Nome, StringComparer.CurrentCulture)
            .ToList();

        return new CatalogoCompletoIFood(groupId, categorias, itens.Count);
    }

    // Leitura campo a campo em vez de desserializar num contrato rígido: o
    // payload real do sellableItems traz null em campos que a doc mostra como
    // número (índices, preço), e um único null num int derrubava a listagem
    // inteira com JsonException.
    private async Task<IReadOnlyList<ItemLido>> ListarItensVendaveisAsync(
        Guid merchantId, string groupId, CancellationToken ct)
    {
        const string operacao = "listar itens vendáveis";

        using var documento = await GetJsonAsync(
            $"merchants/{merchantId}/catalogs/{Uri.EscapeDataString(groupId)}/sellableItems", operacao, ct);

        return ListaDaRaiz(documento.RootElement, operacao)
            .Select(LerItem)
            .OfType<ItemLido>()
            .ToList();
    }

    // Complementar: sem ele a listagem perde só a marcação de pausado, então
    // falha aqui vira log e não derruba o cardápio.
    private async Task<RestricoesDoCatalogo> LerRestricoesAsync(
        Guid merchantId, string catalogId, CancellationToken ct)
    {
        try
        {
            using var documento = await GetJsonAsync(
                $"merchants/{merchantId}/catalogs/{Uri.EscapeDataString(catalogId)}/unsellableItems",
                "listar itens não vendáveis", ct);

            return LerRestricoes(documento.RootElement);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex,
                "Não foi possível ler os itens não vendáveis do catálogo {CatalogId} no iFood; seguindo sem restrições.",
                catalogId);
            return new RestricoesDoCatalogo(new(), new());
        }
    }

    private async Task<JsonDocument> GetJsonAsync(string caminho, string operacao, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(caminho, ct);

        if (!response.IsSuccessStatusCode)
            throw await ToExceptionAsync(response, operacao, ct);

        var corpo = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(corpo))
            return JsonDocument.Parse("[]");

        try
        {
            return JsonDocument.Parse(corpo);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "iFood devolveu corpo que não é JSON ao {Operacao}. Trecho: {Trecho}",
                operacao, Trecho(corpo));
            throw new IFoodApiException(HttpStatusCode.BadGateway, $"O iFood devolveu uma resposta inválida ao {operacao}.");
        }
    }

    // A doc mostra array na raiz. Se vier envelopado num objeto, usa a
    // primeira propriedade que for array em vez de falhar.
    private List<JsonElement> ListaDaRaiz(JsonElement raiz, string operacao)
    {
        switch (raiz.ValueKind)
        {
            case JsonValueKind.Array:
                return raiz.EnumerateArray().ToList();
            case JsonValueKind.Null:
                return [];
            case JsonValueKind.Object:
                foreach (var propriedade in raiz.EnumerateObject())
                {
                    if (propriedade.Value.ValueKind == JsonValueKind.Array)
                        return propriedade.Value.EnumerateArray().ToList();
                }
                break;
        }

        _logger.LogWarning("Formato inesperado do iFood ao {Operacao}. Trecho: {Trecho}",
            operacao, Trecho(raiz.GetRawText()));
        throw new IFoodApiException(HttpStatusCode.BadGateway, $"Formato inesperado do iFood ao {operacao}.");
    }

    private static CatalogoDoIFood? LerCatalogo(JsonElement catalogo)
    {
        var catalogId = Texto(catalogo, "catalogId");
        if (catalogId is null)
            return null;

        return new CatalogoDoIFood(
            CatalogId: catalogId,
            Status: Texto(catalogo, "status"),
            Context: Textos(catalogo, "context"),
            ModifiedAt: Data(catalogo, "modifiedAt"),
            GroupId: Texto(catalogo, "groupId"));
    }

    private static ItemLido? LerItem(JsonElement item)
    {
        var itemId = Texto(item, "itemId");
        if (itemId is null)
            return null;

        var (preco, precoOriginal) = LerPreco(item);

        return new ItemLido(
            ItemId: itemId,
            CategoryId: Texto(item, "categoryId"),
            CategoryName: Texto(item, "categoryName"),
            CategoryIndex: Inteiro(item, "categoryIndex"),
            Nome: Texto(item, "itemName") ?? "(sem nome)",
            Descricao: Texto(item, "itemDescription"),
            ItemIndex: Inteiro(item, "itemIndex"),
            Preco: preco,
            PrecoOriginal: precoOriginal,
            ImagemUrl: Textos(item, "logosUrls").FirstOrDefault());
    }

    // itemPrice = { value, originalValue }, em reais. originalValue só vale
    // como "preço cheio" quando difere do de venda — senão não há promoção.
    private static (decimal Preco, decimal? PrecoOriginal) LerPreco(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("itemPrice", out var preco))
            return (0m, null);

        if (preco.ValueKind != JsonValueKind.Object)
            return (NumeroDe(preco) ?? 0m, null);

        var valor = Numero(preco, "value") ?? 0m;
        var original = Numero(preco, "originalValue");

        return original.HasValue && original.Value > 0m && original.Value != valor
            ? (valor, original)
            : (valor, null);
    }

    private static RestricoesDoCatalogo LerRestricoes(JsonElement raiz)
    {
        var porItem = new Dictionary<string, IReadOnlyList<string>>();
        var porCategoria = new Dictionary<string, IReadOnlyList<string>>();

        if (raiz.ValueKind != JsonValueKind.Object
            || !raiz.TryGetProperty("categories", out var categorias)
            || categorias.ValueKind != JsonValueKind.Array)
            return new RestricoesDoCatalogo(porItem, porCategoria);

        foreach (var categoria in categorias.EnumerateArray())
        {
            if (Texto(categoria, "id") is { } categoriaId)
                porCategoria.TryAdd(categoriaId, Textos(categoria, "restrictions"));

            if (categoria.ValueKind != JsonValueKind.Object
                || !categoria.TryGetProperty("unsellableItems", out var itens)
                || itens.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var item in itens.EnumerateArray())
            {
                if (Texto(item, "id") is { } itemId)
                    porItem.TryAdd(itemId, Textos(item, "restrictions"));
            }
        }

        return new RestricoesDoCatalogo(porItem, porCategoria);
    }

    private static string? Texto(JsonElement objeto, string nome)
    {
        if (objeto.ValueKind != JsonValueKind.Object || !objeto.TryGetProperty(nome, out var valor))
            return null;

        var texto = valor.ValueKind switch
        {
            JsonValueKind.String => valor.GetString(),
            JsonValueKind.Number => valor.GetRawText(),
            _ => null,
        };

        return string.IsNullOrWhiteSpace(texto) ? null : texto;
    }

    private static IReadOnlyList<string> Textos(JsonElement objeto, string nome)
    {
        if (objeto.ValueKind != JsonValueKind.Object
            || !objeto.TryGetProperty(nome, out var valor)
            || valor.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        return valor.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString())
            .OfType<string>()
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    private static decimal? Numero(JsonElement objeto, string nome) =>
        objeto.ValueKind == JsonValueKind.Object && objeto.TryGetProperty(nome, out var valor)
            ? NumeroDe(valor)
            : null;

    private static decimal? NumeroDe(JsonElement valor)
    {
        if (valor.ValueKind == JsonValueKind.Number && valor.TryGetDecimal(out var numero))
            return numero;

        if (valor.ValueKind == JsonValueKind.String
            && decimal.TryParse(valor.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var convertido))
            return convertido;

        return null;
    }

    private static DateTimeOffset? Data(JsonElement objeto, string nome) =>
        Texto(objeto, nome) is { } texto
        && DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var data)
            ? data
            : null;

    // Sem índice o item vai para o fim da lista, não para o topo.
    private static int Inteiro(JsonElement objeto, string nome) =>
        Numero(objeto, nome) is { } valor && valor >= int.MinValue && valor <= int.MaxValue
            ? (int)valor
            : int.MaxValue;

    private static string Trecho(string corpo) =>
        corpo.Length <= TamanhoDoTrechoNoLog ? corpo : corpo[..TamanhoDoTrechoNoLog] + "…";

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

    private sealed record ItemLido(
        string ItemId,
        string? CategoryId,
        string? CategoryName,
        int CategoryIndex,
        string Nome,
        string? Descricao,
        int ItemIndex,
        decimal Preco,
        decimal? PrecoOriginal,
        string? ImagemUrl);

    private sealed record RestricoesDoCatalogo(
        Dictionary<string, IReadOnlyList<string>> PorItem,
        Dictionary<string, IReadOnlyList<string>> PorCategoria);
}
