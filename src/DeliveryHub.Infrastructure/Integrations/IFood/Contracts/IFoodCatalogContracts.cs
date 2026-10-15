using System.Text.Json.Serialization;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Contracts;

// Contratos do módulo Catalog do iFood. Ficam separados dos demais porque o
// envelope de erro é outro: Auth/Order/Events devolvem
// { "error": { code, message } } (ver IFoodErrorResponse), enquanto Catalog
// devolve o objeto plano com requestId e details[]. Desserializar um com o
// contrato do outro devolve campos nulos silenciosamente.

internal sealed class IFoodCatalog
{
    [JsonPropertyName("catalogId")]
    public string CatalogId { get; init; } = string.Empty;

    // AVAILABLE é o único valor confirmado na doc até agora. String em vez de
    // enum para não quebrar quando o iFood introduzir outro status — o mapper
    // decide o que fazer com valor desconhecido.
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    // Onde o catálogo vale: DEFAULT (delivery) e INDOOR (consumo no local) são
    // os valores vistos na doc. Um merchant pode ter catálogo só para INDOOR,
    // que não interessa ao despacho de entrega.
    [JsonPropertyName("context")]
    public IReadOnlyList<string> Context { get; init; } = [];

    [JsonPropertyName("modifiedAt")]
    public DateTimeOffset? ModifiedAt { get; init; }

    [JsonPropertyName("groupId")]
    public string? GroupId { get; init; }
}

// Item vendável do catálogo (GET /merchants/{id}/catalogs/{groupId}/sellableItems).
// Só vem o que pode ser vendido agora — pausado, com violação ou fora de agenda
// é filtrado pelo iFood e aparece em /unsellableItems. O campo itemPrice tem
// value (preço de venda) e originalValue (preço cheio, antes do desconto).
internal sealed class IFoodSellableItem
{
    [JsonPropertyName("itemId")]
    public string ItemId { get; init; } = string.Empty;

    [JsonPropertyName("categoryId")]
    public string? CategoryId { get; init; }

    [JsonPropertyName("categoryName")]
    public string? CategoryName { get; init; }

    [JsonPropertyName("categoryIndex")]
    public int CategoryIndex { get; init; }

    [JsonPropertyName("itemName")]
    public string? ItemName { get; init; }

    [JsonPropertyName("itemDescription")]
    public string? ItemDescription { get; init; }

    [JsonPropertyName("itemIndex")]
    public int ItemIndex { get; init; }

    [JsonPropertyName("itemEan")]
    public string? ItemEan { get; init; }

    [JsonPropertyName("itemExternalCode")]
    public string? ItemExternalCode { get; init; }

    [JsonPropertyName("itemPrice")]
    public IFoodItemPrice? ItemPrice { get; init; }

    // Preços em reais, com até 2 casas decimais — a doc do iFood confirma que
    // não é centavo, diferente da 99Food. Nullable porque bebida avulsa não
    // tem preço mínimo definido.
    [JsonPropertyName("itemMinSalePrice")]
    public decimal? ItemMinSalePrice { get; init; }

    [JsonPropertyName("logosUrls")]
    public IReadOnlyList<string> LogosUrls { get; init; } = [];
}

internal sealed class IFoodItemPrice
{
    [JsonPropertyName("value")]
    public decimal Value { get; init; }

    [JsonPropertyName("originalValue")]
    public decimal OriginalValue { get; init; }
}

// Envelope de /unsellableItems: lista de categorias contendo os itens em
// problema. Importa pro painel saber quais estão pausados ou com violação
// mesmo que o /sellableItems já tenha filtrado — é o que explica para o
// lojista por que o item não aparece no app.
internal sealed class IFoodUnsellableResponse
{
    [JsonPropertyName("categories")]
    public IReadOnlyList<IFoodUnsellableCategory> Categories { get; init; } = [];
}

internal sealed class IFoodUnsellableCategory
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    // CATEGORY_PAUSED, HAS_VIOLATION — o enum do iFood lista mais, mas string
    // simples mantém a mensagem exata que o portal mostra.
    [JsonPropertyName("restrictions")]
    public IReadOnlyList<string> Restrictions { get; init; } = [];

    [JsonPropertyName("unsellableItems")]
    public IReadOnlyList<IFoodUnsellableItem> UnsellableItems { get; init; } = [];
}

internal sealed class IFoodUnsellableItem
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("productId")]
    public string? ProductId { get; init; }

    // ITEM_PAUSED, ITEM_HAS_VIOLATION.
    [JsonPropertyName("restrictions")]
    public IReadOnlyList<string> Restrictions { get; init; } = [];
}

// Envelope de erro do módulo Catalog.
internal sealed class IFoodCatalogErrorResponse
{
    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    // UUID do request. O suporte do iFood pede este valor para rastrear a
    // chamada, então precisa sobreviver até o log.
    [JsonPropertyName("requestId")]
    public string? RequestId { get; init; }

    [JsonPropertyName("details")]
    public IReadOnlyList<IFoodCatalogErrorDetail> Details { get; init; } = [];

    // Texto pronto para log e para a mensagem da exceção: sem os details, um
    // 400 de validação não diz qual campo recusou.
    public string Resumir()
    {
        var baseMsg = $"{Code ?? "(sem código)"}: {Message ?? "(sem mensagem)"}";
        if (RequestId is not null)
            baseMsg += $" [requestId={RequestId}]";

        if (Details.Count == 0)
            return baseMsg;

        var campos = string.Join("; ", Details.Select(d => $"{d.Field}={d.Code} ({d.Message})"));
        return $"{baseMsg} — {campos}";
    }
}

internal sealed class IFoodCatalogErrorDetail
{
    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("field")]
    public string? Field { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
