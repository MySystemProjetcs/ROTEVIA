using System.Net.Http.Json;
using System.Text.Json;
using DeliveryHub.Infrastructure.Integrations.DiDiFood.Auth;
using DeliveryHub.Infrastructure.Integrations.DiDiFood.Contracts;

namespace DeliveryHub.Infrastructure.Integrations.DiDiFood.Catalog;

// DTOs que cruzam a fronteira do assembly. Não são os contratos da 99Food:
// aqueles moram em Contracts e são internal por regra de arquitetura testada.
// Preço aqui já é decimal em reais — a conversão de centavos acontece no
// client, para que ninguém fora dele precise lembrar da unidade.
public sealed record ItemDoCardapio99(
    string AppItemId,
    string Nome,
    string? Descricao,
    decimal Preco,
    bool Disponivel,
    bool VendidoSeparadamente,
    string? CategoriaId,
    string? ImagemUrl);

public sealed record CategoriaDoCardapio99(
    string AppCategoryId,
    string Nome,
    IReadOnlyList<ItemDoCardapio99> Itens);

public sealed record CardapioDa99(
    IReadOnlyList<CategoriaDoCardapio99> Categorias,
    IReadOnlyList<ItemDoCardapio99> ItensSemCategoria,
    int TotalItens);

// Resultado parcial: a 99 aceita parte do lote e recusa o resto.
public sealed record ResultadoAtualizacaoStatus(
    IReadOnlyList<string> Sucesso,
    IReadOnlyList<string> Falha);

public interface IDiDiFoodCatalogClient
{
    Task<CardapioDa99> ObterCardapioAsync(string appShopId, string? appId, CancellationToken ct);

    // status: true = disponível no app, false = indisponível. A 99 usa 1 e 2;
    // a tradução fica aqui para não espalhar número mágico.
    Task<ResultadoAtualizacaoStatus> DefinirDisponibilidadeAsync(
        string appShopId, string? appId, IReadOnlyList<string> appItemIds, bool disponivel, CancellationToken ct);
}

internal sealed class DiDiFoodCatalogClient : IDiDiFoodCatalogClient
{
    private const int StatusDisponivel = 1;
    private const int StatusIndisponivel = 2;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDiDiFoodAuthenticator _autenticador;

    public DiDiFoodCatalogClient(
        IHttpClientFactory httpClientFactory, IDiDiFoodAuthenticator autenticador)
    {
        _httpClientFactory = httpClientFactory;
        _autenticador = autenticador;
    }

    public async Task<CardapioDa99> ObterCardapioAsync(
        string appShopId, string? appId, CancellationToken ct)
    {
        var token = await _autenticador.GetAuthTokenAsync(appShopId, appId, ct);
        var http = _httpClientFactory.CreateClient(nameof(DiDiFoodCatalogClient));

        var uri = $"/v3/item/item/list?auth_token={Uri.EscapeDataString(token)}";
        var detalhe = await ExecutarAsync<DiDiMenuDetalhe>(http, HttpMethod.Get, uri, null, "item/list", ct);

        return Montar(detalhe);
    }

    public async Task<ResultadoAtualizacaoStatus> DefinirDisponibilidadeAsync(
        string appShopId,
        string? appId,
        IReadOnlyList<string> appItemIds,
        bool disponivel,
        CancellationToken ct)
    {
        if (appItemIds.Count == 0)
            return new ResultadoAtualizacaoStatus([], []);

        var token = await _autenticador.GetAuthTokenAsync(appShopId, appId, ct);
        var http = _httpClientFactory.CreateClient(nameof(DiDiFoodCatalogClient));

        var corpo = new DiDiAtualizarStatusItens(
            token, appItemIds, disponivel ? StatusDisponivel : StatusIndisponivel);

        var resultado = await ExecutarAsync<DiDiResultadoStatusItens>(
            http, HttpMethod.Post, "/v3/item/item/updateItemStatus", corpo, "updateItemStatus", ct);

        // Resposta sem corpo: a doc marca success/failed como opcionais. Sem
        // informação do contrário, assumir que tudo passou seria mentir — mas
        // a chamada teve errno 0, então o caminho honesto é creditar o lote.
        return resultado is null
            ? new ResultadoAtualizacaoStatus(appItemIds, [])
            : new ResultadoAtualizacaoStatus(resultado.Success, resultado.Failed);
    }

    // Agrupa itens por categoria. Item sem categoria é caso real na 99 (a doc
    // diz "can only belong to one category or no category"), e some da tela se
    // o agrupamento ignorar — por isso sai em lista própria.
    private static CardapioDa99 Montar(DiDiMenuDetalhe? detalhe)
    {
        if (detalhe is null)
            return new CardapioDa99([], [], 0);

        var itens = detalhe.Items
            .Where(i => !string.IsNullOrWhiteSpace(i.AppItemId))
            .Select(Traduzir)
            .ToList();

        var porCategoria = itens
            .Where(i => i.CategoriaId is not null)
            .GroupBy(i => i.CategoriaId!)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ItemDoCardapio99>)g.ToList());

        var categorias = detalhe.Categories
            .Where(c => !string.IsNullOrWhiteSpace(c.AppCategoryId))
            .Select(c => new CategoriaDoCardapio99(
                c.AppCategoryId!,
                c.CategoryName ?? "Sem nome",
                porCategoria.TryGetValue(c.AppCategoryId!, out var lista) ? lista : []))
            .ToList();

        var categoriasConhecidas = categorias.Select(c => c.AppCategoryId).ToHashSet();
        var orfaos = itens
            .Where(i => i.CategoriaId is null || !categoriasConhecidas.Contains(i.CategoriaId))
            .ToList();

        return new CardapioDa99(categorias, orfaos, itens.Count);
    }

    private static ItemDoCardapio99 Traduzir(DiDiItem item) => new(
        AppItemId: item.AppItemId!,
        Nome: item.ItemName ?? "Sem nome",
        Descricao: item.ShortDesc,
        // Centavos para reais. A 99 devolve na menor unidade da moeda.
        Preco: item.Price / 100m,
        // Status ausente no list: a doc não garante o campo. Tratar ausência
        // como indisponível esconderia item ativo, então o padrão é disponível.
        Disponivel: item.Status is null or StatusDisponivel,
        VendidoSeparadamente: item.IsSoldSeparately,
        CategoriaId: string.IsNullOrWhiteSpace(item.AppCategoryId) ? null : item.AppCategoryId,
        ImagemUrl: item.HeadImg);

    // errno != 0 chega com HTTP 200 — tratar só o status deixaria erro de
    // negócio passar por sucesso.
    private static async Task<T?> ExecutarAsync<T>(
        HttpClient http,
        HttpMethod metodo,
        string uri,
        object? corpo,
        string operacao,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(metodo, uri);
        if (corpo is not null)
            request.Content = JsonContent.Create(corpo, corpo.GetType());

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        DiDiStandardResponse<T>? envelope;
        try
        {
            envelope = await response.Content.ReadFromJsonAsync<DiDiStandardResponse<T>>(cancellationToken: ct);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"99Food devolveu corpo ilegível em {operacao}: {ex.Message}", ex);
        }

        if (envelope is null)
            throw new InvalidOperationException($"Resposta vazia da 99Food em {operacao}.");

        if (!envelope.Ok)
            throw new InvalidOperationException(envelope.Resumir(operacao));

        return envelope.Data;
    }
}
