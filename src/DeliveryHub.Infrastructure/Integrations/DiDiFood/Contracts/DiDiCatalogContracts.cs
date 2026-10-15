using System.Text.Json.Serialization;

namespace DeliveryHub.Infrastructure.Integrations.DiDiFood.Contracts;

// Contratos do cardápio da 99Food, transcritos do swagger oficial
// (openapi.didi-food.com). Usamos a v3 porque a própria doc a chama de
// "recommend API to upload store menu": ela modela modifier groups de
// verdade, enquanto a v1 aninha sub-itens e limita a profundidade.
//
// Preço é SEMPRE inteiro na menor unidade da moeda ("set in the lowest
// denomination (e.g., cents for MX currency, not Peso)") — em BRL, centavos.
// Converter para decimal é trabalho do mapper, nunca deste contrato.

internal sealed class DiDiMenuDetalhe
{
    [JsonPropertyName("menus")]
    public IReadOnlyList<DiDiMenu> Menus { get; init; } = [];

    [JsonPropertyName("categories")]
    public IReadOnlyList<DiDiCategoria> Categories { get; init; } = [];

    [JsonPropertyName("items")]
    public IReadOnlyList<DiDiItem> Items { get; init; } = [];

    [JsonPropertyName("modifier_groups")]
    public IReadOnlyList<DiDiModifierGroup> ModifierGroups { get; init; } = [];
}

// A doc avisa que só um menu é suportado hoje: "Currently, only one menu is
// supported. Multiple menus need to be mapped to 1 menu".
internal sealed class DiDiMenu
{
    [JsonPropertyName("app_menu_id")]
    public string? AppMenuId { get; init; }

    [JsonPropertyName("menu_name")]
    public string? MenuName { get; init; }
}

internal sealed class DiDiCategoria
{
    [JsonPropertyName("app_category_id")]
    public string? AppCategoryId { get; init; }

    [JsonPropertyName("category_name")]
    public string? CategoryName { get; init; }

    [JsonPropertyName("app_menu_id")]
    public string? AppMenuId { get; init; }
}

internal sealed class DiDiItem
{
    // Id do item no NOSSO sistema — a 99 mapeia direto, sem id próprio
    // ("ID for the item, provided by the restaurant or POS. Map directly").
    [JsonPropertyName("app_item_id")]
    public string? AppItemId { get; init; }

    [JsonPropertyName("app_external_id")]
    public string? AppExternalId { get; init; }

    [JsonPropertyName("item_name")]
    public string? ItemName { get; init; }

    [JsonPropertyName("short_desc")]
    public string? ShortDesc { get; init; }

    [JsonPropertyName("head_img")]
    public string? HeadImg { get; init; }

    // Centavos. Ver nota no topo do arquivo.
    [JsonPropertyName("price")]
    public int Price { get; init; }

    // False = só aparece dentro de um modifier group, nunca sozinho no app.
    [JsonPropertyName("is_sold_separately")]
    public bool IsSoldSeparately { get; init; }

    [JsonPropertyName("app_category_id")]
    public string? AppCategoryId { get; init; }

    // A doc não documenta este campo no list da v3, mas ele aparece no
    // updateItemStatus (1 online, 2 offline). Nullable para não inventar
    // valor quando a resposta não trouxer.
    [JsonPropertyName("status")]
    public int? Status { get; init; }
}

internal sealed class DiDiModifierGroup
{
    [JsonPropertyName("app_modifier_group_id")]
    public string? AppModifierGroupId { get; init; }

    [JsonPropertyName("modifier_group_name")]
    public string? ModifierGroupName { get; init; }

    // 1 obrigatório, 2 opcional — invertido em relação ao que a intuição
    // sugere, então o mapper não pode tratar como booleano direto.
    [JsonPropertyName("is_required")]
    public int? IsRequired { get; init; }

    [JsonPropertyName("quantity_min_permitted")]
    public int? QuantityMinPermitted { get; init; }

    [JsonPropertyName("quantity_max_permitted")]
    public int? QuantityMaxPermitted { get; init; }

    [JsonPropertyName("app_mg_items")]
    public IReadOnlyList<DiDiModifierGroupItem> AppMgItems { get; init; } = [];
}

internal sealed class DiDiModifierGroupItem
{
    [JsonPropertyName("app_item_id")]
    public string? AppItemId { get; init; }

    [JsonPropertyName("price")]
    public int? Price { get; init; }
}

// Corpo de POST /v3/item/item/updateItemStatus. Aceita lote de ids, ao
// contrário da v1, que só muda um item por chamada.
internal sealed record DiDiAtualizarStatusItens(
    [property: JsonPropertyName("auth_token")] string AuthToken,
    [property: JsonPropertyName("app_item_ids")] IReadOnlyList<string> AppItemIds,
    [property: JsonPropertyName("status")] int Status);

// Resposta do updateItemStatus da v3: a 99 aceita parcialmente, devolvendo
// quais ids passaram e quais não. Ignorar o failed daria "salvo com sucesso"
// para item que continuou como estava.
internal sealed class DiDiResultadoStatusItens
{
    [JsonPropertyName("success")]
    public IReadOnlyList<string> Success { get; init; } = [];

    [JsonPropertyName("failed")]
    public IReadOnlyList<string> Failed { get; init; } = [];
}
