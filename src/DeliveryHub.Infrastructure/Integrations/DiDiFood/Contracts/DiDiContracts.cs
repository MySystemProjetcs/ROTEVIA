using System.Text.Json.Serialization;

namespace DeliveryHub.Infrastructure.Integrations.DiDiFood.Contracts;

// Envelope do webhook da DiDi Food. A DiDi envia um POST com este corpo
// para a URL cadastrada no painel de desenvolvedor quando o status de um
// pedido muda.
//
// Campos mapeados da spec openapi.didi-food.com — todos os campos do
// OrderModel que são relevantes para o fluxo de ingestão do DeliveryHub.
// Campos não usados no ACL estão presentes para que o fixture fique íntegro
// e o processador futuro não precise reler a spec.
internal sealed class DiDiWebhookEvent
{
    // Tipo do evento: "newOrder", "cancelOrder", "orderStatusChange", etc.
    // A DiDi não documenta o envelope explicitamente — capturamos tudo e
    // deixamos o processador decidir o que fazer com cada tipo.
    [JsonPropertyName("event_type")]
    public string? EventType { get; init; }

    // O order_id é o identificador estável do evento para idempotência.
    // Dois webhooks com o mesmo order_id + event_type = mesmo evento.
    [JsonPropertyName("order_id")]
    public long OrderId { get; init; }

    // Payload completo do pedido — presente nos eventos de novo pedido.
    [JsonPropertyName("order")]
    public DiDiOrderModel? Order { get; init; }

    // Timestamp unix do envio — usado na verificação de assinatura.
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; }

    // Assinatura MD5 calculada pela DiDi: MD5(app_id + timestamp + app_secret).
    [JsonPropertyName("sign")]
    public string? Sign { get; init; }
}

internal sealed class DiDiOrderModel
{
    [JsonPropertyName("order_id")]
    public long OrderId { get; init; }

    // Status numérico da DiDi. Não é o mesmo que o StatusPedido do domínio —
    // o ACL faz a tradução.
    [JsonPropertyName("status")]
    public int Status { get; init; }

    [JsonPropertyName("before_status")]
    public int BeforeStatus { get; init; }

    // Número do pedido na loja no dia, começa em 1. É o NumeroExibicao.
    [JsonPropertyName("order_index")]
    public int OrderIndex { get; init; }

    [JsonPropertyName("remark")]
    public string? Remark { get; init; }

    [JsonPropertyName("country")]
    public string? Country { get; init; }

    [JsonPropertyName("timezone")]
    public string? Timezone { get; init; }

    // 1: online; 2: dinheiro; 3: POS (cartão); 4: carteira DiDi
    [JsonPropertyName("pay_type")]
    public int PayType { get; init; }

    // 1: entrega pela DiDi; 2: entrega pela loja
    [JsonPropertyName("delivery_type")]
    public int DeliveryType { get; init; }

    // Unix timestamp de criação do pedido
    [JsonPropertyName("create_time")]
    public long CreateTime { get; init; }

    [JsonPropertyName("pay_time")]
    public long PayTime { get; init; }

    [JsonPropertyName("complete_time")]
    public long CompleteTime { get; init; }

    [JsonPropertyName("cancel_time")]
    public long CancelTime { get; init; }

    [JsonPropertyName("shop_confirm_time")]
    public long ShopConfirmTime { get; init; }

    // Tempo estimado para o pedido estar pronto (unix timestamp)
    [JsonPropertyName("expected_cook_eta")]
    public long ExpectedCookEta { get; init; }

    // Tempo estimado de chegada ao cliente (unix timestamp)
    [JsonPropertyName("expected_arrived_eta")]
    public long ExpectedArrivedEta { get; init; }

    [JsonPropertyName("price")]
    public DiDiPriceModel? Price { get; init; }

    [JsonPropertyName("shop")]
    public DiDiOrderShopModel? Shop { get; init; }

    [JsonPropertyName("receive_address")]
    public DiDiCustomerAddressModel? ReceiveAddress { get; init; }

    [JsonPropertyName("order_items")]
    public IReadOnlyList<DiDiOrderItemModel> OrderItems { get; init; } = [];

    [JsonPropertyName("promotions")]
    public IReadOnlyList<DiDiPromotionModel> Promotions { get; init; } = [];
}

internal sealed class DiDiPriceModel
{
    // Preço original dos itens, em centavos
    [JsonPropertyName("order_price")]
    public int OrderPrice { get; init; }

    // Preço real a pagar (após descontos, sem cupom)
    [JsonPropertyName("real_price")]
    public int RealPrice { get; init; }

    // Preço final com cupom
    [JsonPropertyName("real_pay_price")]
    public int RealPayPrice { get; init; }

    // Taxa de entrega real
    [JsonPropertyName("delivery_price")]
    public int DeliveryPrice { get; init; }

    [JsonPropertyName("refund_price")]
    public int RefundPrice { get; init; }

    // Código da moeda — ex: "MXN", "BRL"
    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    [JsonPropertyName("items_discount")]
    public int ItemsDiscount { get; init; }

    [JsonPropertyName("delivery_discount")]
    public int DeliveryDiscount { get; init; }

    [JsonPropertyName("customer_need_paying_money")]
    public int CustomerNeedPayingMoney { get; init; }
}

internal sealed class DiDiOrderShopModel
{
    [JsonPropertyName("shop_id")]
    public long ShopId { get; init; }

    // ID da loja no sistema do parceiro — é o app_shop_id cadastrado.
    // É por ele que resolvemos o MerchantId interno.
    [JsonPropertyName("app_shop_id")]
    public string? AppShopId { get; init; }

    [JsonPropertyName("shop_name")]
    public string? ShopName { get; init; }

    [JsonPropertyName("shop_addr")]
    public string? ShopAddr { get; init; }
}

internal sealed class DiDiCustomerAddressModel
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; init; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; init; }

    [JsonPropertyName("calling_code")]
    public string? CallingCode { get; init; }

    [JsonPropertyName("phone")]
    public string? Phone { get; init; }

    [JsonPropertyName("city")]
    public string? City { get; init; }

    [JsonPropertyName("country_code")]
    public string? CountryCode { get; init; }

    [JsonPropertyName("poi_address")]
    public string? PoiAddress { get; init; }

    [JsonPropertyName("house_number")]
    public string? HouseNumber { get; init; }

    [JsonPropertyName("poi_lat")]
    public string? PoiLat { get; init; }

    [JsonPropertyName("poi_lng")]
    public string? PoiLng { get; init; }

    [JsonPropertyName("poi_display_name")]
    public string? PoiDisplayName { get; init; }
}

internal sealed class DiDiOrderItemModel
{
    // ID do item no sistema do parceiro
    [JsonPropertyName("app_item_id")]
    public string? AppItemId { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    // Preço total da linha (quantidade × preço unitário), em centavos
    [JsonPropertyName("total_price")]
    public int TotalPrice { get; init; }

    [JsonPropertyName("sku_price")]
    public int SkuPrice { get; init; }

    [JsonPropertyName("amount")]
    public int Amount { get; init; }

    [JsonPropertyName("remark")]
    public string? Remark { get; init; }

    // Subitens (modificadores/adicionais do item)
    [JsonPropertyName("sub_item_list")]
    public IReadOnlyList<DiDiOrderSubItemModel> SubItemList { get; init; } = [];

    // Preço após desconto de promoção do item
    [JsonPropertyName("real_price")]
    public int RealPrice { get; init; }
}

internal sealed class DiDiOrderSubItemModel
{
    [JsonPropertyName("app_item_id")]
    public string? AppItemId { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("total_price")]
    public int TotalPrice { get; init; }

    [JsonPropertyName("sku_price")]
    public int SkuPrice { get; init; }

    [JsonPropertyName("amount")]
    public int Amount { get; init; }

    [JsonPropertyName("sub_item_list")]
    public IReadOnlyList<DiDiOrderSubItemModel> SubItemList { get; init; } = [];
}

internal sealed class DiDiPromotionModel
{
    // 0: sem promoção; 1: desconto; 2: preço especial; 3: entrega grátis;
    // 4: compre X ganhe Y; 10: cupom no pedido; 12: cupom na entrega
    [JsonPropertyName("promo_type")]
    public int PromoType { get; init; }

    [JsonPropertyName("save_price")]
    public int SavePrice { get; init; }

    [JsonPropertyName("shop_subside_price")]
    public int ShopSubsidePrice { get; init; }
}
