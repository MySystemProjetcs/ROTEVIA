using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Contracts;

// GET /order/v1.0/orders/{id} — payload do pedido no formato do iFood.
// Nenhum destes tipos pode aparecer em Domain ou Application: o mapeamento
// para Pedido acontece no IFoodOrderMapper (CLAUDE.md §4).
// Campos de mercado (picking, scalePrices) ficam de fora — são de grocery,
// fora do escopo de delivery de restaurante. JSON desconhecido é ignorado.
internal sealed record IFoodOrderDetails(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("displayId")] string DisplayId,
    [property: JsonPropertyName("orderType")] string OrderType,
    [property: JsonPropertyName("orderTiming")] string OrderTiming,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("preparationStartDateTime")] DateTimeOffset PreparationStartDateTime,
    [property: JsonPropertyName("merchant")] IFoodOrderMerchant Merchant,
    [property: JsonPropertyName("customer")] IFoodCustomer Customer,
    [property: JsonPropertyName("items")] IReadOnlyList<IFoodOrderItem> Items,
    [property: JsonPropertyName("total")] IFoodOrderTotal Total,
    [property: JsonPropertyName("payments")] IFoodPayments Payments,
    [property: JsonPropertyName("delivery")] IFoodDeliveryInformation? Delivery,
    [property: JsonPropertyName("schedule")] IFoodScheduleInformation? Schedule,
    [property: JsonPropertyName("indoor")] IFoodIndoorInformation? Indoor,
    [property: JsonPropertyName("dineIn")] IFoodDineInInformation? DineIn,
    [property: JsonPropertyName("takeout")] IFoodTakeoutInformation? Takeout,
    [property: JsonPropertyName("benefits")] IReadOnlyList<IFoodBenefit>? Benefits,
    [property: JsonPropertyName("additionalFees")] IReadOnlyList<IFoodAdditionalFee>? AdditionalFees,
    [property: JsonPropertyName("salesChannel")] string? SalesChannel,
    [property: JsonPropertyName("category")] string? Category,
    // Não está no schema publicado, mas vem no payload real e marca pedido de
    // sandbox. Precisa chegar ao domínio: pedido de teste não pode gerar
    // lançamento no ledger nem cobrança.
    [property: JsonPropertyName("isTest")] bool IsTest,
    [property: JsonPropertyName("additionalInfo")] IFoodAdditionalInfo? AdditionalInfo,
    [property: JsonPropertyName("picking")] IFoodPicking? Picking);

// O schema diz "extraInfo" (string); o payload real traz "additionalInfo" objeto.
internal sealed record IFoodAdditionalInfo(
    [property: JsonPropertyName("metadata")] JsonElement? Metadata);

internal sealed record IFoodPicking(
    [property: JsonPropertyName("picker")] string? Picker,
    [property: JsonPropertyName("replacementOptions")] string? ReplacementOptions);

internal sealed record IFoodOrderMerchant(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("business")] IFoodMerchantBusiness? Business);

internal sealed record IFoodMerchantBusiness(
    [property: JsonPropertyName("name")] string? Name);

internal sealed record IFoodCustomer(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("ordersCountOnMerchant")] int OrdersCountOnMerchant,
    [property: JsonPropertyName("documentNumber")] string? DocumentNumber,
    [property: JsonPropertyName("documentType")] string? DocumentType,
    [property: JsonPropertyName("segmentation")] string? Segmentation,
    [property: JsonPropertyName("phone")] IFoodPhone? Phone);

internal sealed record IFoodPhone(
    [property: JsonPropertyName("number")] string Number,
    [property: JsonPropertyName("localizer")] string? Localizer,
    [property: JsonPropertyName("localizerExpiration")] DateTimeOffset? LocalizerExpiration);

internal sealed record IFoodOrderItem(
    [property: JsonPropertyName("id")] Guid Id,
    // Identifica a linha do pedido: é por uniqueId que os eventos
    // ORDER_PATCHED referenciam o item alterado.
    [property: JsonPropertyName("uniqueId")] string? UniqueId,
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("unit")] string Unit,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("unitPrice")] decimal UnitPrice,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("optionsPrice")] decimal OptionsPrice,
    [property: JsonPropertyName("customizationPrice")] decimal CustomizationPrice,
    [property: JsonPropertyName("totalPrice")] decimal TotalPrice,
    [property: JsonPropertyName("options")] IReadOnlyList<IFoodOrderItemOption>? Options,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("externalCode")] string? ExternalCode,
    [property: JsonPropertyName("ean")] string? Ean,
    [property: JsonPropertyName("observations")] string? Observations,
    [property: JsonPropertyName("imageUrl")] string? ImageUrl);

internal sealed record IFoodOrderItemOption(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("unit")] string Unit,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("unitPrice")] decimal UnitPrice,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("addition")] decimal Addition,
    [property: JsonPropertyName("groupName")] string? GroupName,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("customization")] IReadOnlyList<IFoodCustomization>? Customization,
    [property: JsonPropertyName("externalCode")] string? ExternalCode,
    [property: JsonPropertyName("ean")] string? Ean);

internal sealed record IFoodCustomization(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("groupName")] string GroupName,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("unitPrice")] decimal UnitPrice,
    [property: JsonPropertyName("addition")] decimal Addition,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("externalCode")] string? ExternalCode);

internal sealed record IFoodOrderTotal(
    [property: JsonPropertyName("orderAmount")] decimal OrderAmount,
    [property: JsonPropertyName("subTotal")] decimal SubTotal,
    [property: JsonPropertyName("deliveryFee")] decimal DeliveryFee,
    [property: JsonPropertyName("benefits")] decimal Benefits,
    [property: JsonPropertyName("additionalFees")] decimal AdditionalFees);

internal sealed record IFoodPayments(
    [property: JsonPropertyName("prepaid")] decimal Prepaid,
    [property: JsonPropertyName("pending")] decimal Pending,
    [property: JsonPropertyName("methods")] IReadOnlyList<IFoodPaymentMethod> Methods);

internal sealed record IFoodPaymentMethod(
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("value")] decimal Value,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("prepaid")] bool Prepaid,
    [property: JsonPropertyName("cash")] IFoodCashInformation? Cash,
    [property: JsonPropertyName("card")] IFoodCardInformation? Card,
    [property: JsonPropertyName("wallet")] IFoodWalletInformation? Wallet,
    [property: JsonPropertyName("transaction")] IFoodTransactionInformation? Transaction);

// Dados de NFe. Guardar com cuidado: acquirerDocument é CNPJ.
internal sealed record IFoodTransactionInformation(
    [property: JsonPropertyName("authorizationCode")] string AuthorizationCode,
    [property: JsonPropertyName("acquirerDocument")] string AcquirerDocument);

internal sealed record IFoodCashInformation(
    [property: JsonPropertyName("changeFor")] decimal ChangeFor);

internal sealed record IFoodCardInformation(
    [property: JsonPropertyName("brand")] string Brand);

internal sealed record IFoodWalletInformation(
    [property: JsonPropertyName("name")] string Name);

internal sealed record IFoodDeliveryInformation(
    // mode é controle interno do iFood (PRIORITY, TURBO, HIGH_DENSITY, DEFAULT,
    // EXPRESS); description é o que o cliente pediu (Padrão, Rápida, Expressa).
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("deliveredBy")] string DeliveredBy,
    [property: JsonPropertyName("deliveryDateTime")] DateTimeOffset DeliveryDateTime,
    [property: JsonPropertyName("pickupCode")] string PickupCode,
    [property: JsonPropertyName("observations")] string? Observations,
    [property: JsonPropertyName("deliveryAddress")] IFoodDeliveryAddress DeliveryAddress);

internal sealed record IFoodDeliveryAddress(
    [property: JsonPropertyName("formattedAddress")] string FormattedAddress,
    [property: JsonPropertyName("streetName")] string StreetName,
    [property: JsonPropertyName("streetNumber")] string StreetNumber,
    [property: JsonPropertyName("city")] string City,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("country")] string Country,
    [property: JsonPropertyName("postalCode")] string PostalCode,
    [property: JsonPropertyName("coordinates")] IFoodCoordinates Coordinates,
    [property: JsonPropertyName("neighborhood")] string? Neighborhood,
    [property: JsonPropertyName("complement")] string? Complement,
    [property: JsonPropertyName("reference")] string? Reference);

internal sealed record IFoodCoordinates(
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude);

internal sealed record IFoodScheduleInformation(
    [property: JsonPropertyName("deliveryDateTimeStart")] DateTimeOffset DeliveryDateTimeStart,
    [property: JsonPropertyName("deliveryDateTimeEnd")] DateTimeOffset DeliveryDateTimeEnd);

internal sealed record IFoodIndoorInformation(
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("deliveryDateTime")] DateTimeOffset DeliveryDateTime,
    [property: JsonPropertyName("table")] string? Table);

internal sealed record IFoodDineInInformation(
    [property: JsonPropertyName("deliveryDateTime")] DateTimeOffset DeliveryDateTime);

internal sealed record IFoodTakeoutInformation(
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("takeoutDateTime")] DateTimeOffset TakeoutDateTime);

internal sealed record IFoodBenefit(
    [property: JsonPropertyName("value")] decimal Value,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("targetId")] string? TargetId,
    [property: JsonPropertyName("sponsorshipValues")] IReadOnlyList<IFoodSponsorship> SponsorshipValues,
    [property: JsonPropertyName("campaign")] IFoodCampaign? Campaign);

internal sealed record IFoodSponsorship(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("value")] decimal Value,
    [property: JsonPropertyName("description")] string Description);

internal sealed record IFoodCampaign(
    [property: JsonPropertyName("id")] Guid? Id,
    [property: JsonPropertyName("name")] string? Name);

internal sealed record IFoodAdditionalFee(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("value")] decimal Value,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("fullDescription")] string FullDescription,
    [property: JsonPropertyName("liabilities")] IReadOnlyList<IFoodLiability>? Liabilities);

internal sealed record IFoodLiability(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("percentage")] decimal Percentage);

// Corpo e resposta do verifyDeliveryCode. Ficam aqui junto dos demais
// contratos do pedido, e como todo Contracts/ não podem vazar para Application.
internal sealed record IFoodVerifyDeliveryCodeRequest(
    [property: JsonPropertyName("code")] string Code);

internal sealed record IFoodVerifyDeliveryCodeResponse(
    [property: JsonPropertyName("valid")] bool Valid);

// Corpo do requestCancellation. cancellationCode é o código do iFood (varia por
// pedido — a lista real vem de GET orders/{id}/cancellationReasons); reason é o
// texto que o lojista informou.
internal sealed record IFoodRequestCancellationRequest(
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("cancellationCode")] string CancellationCode);
