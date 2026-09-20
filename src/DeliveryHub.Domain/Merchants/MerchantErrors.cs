using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Domain.Merchants;

public static class MerchantErrors
{
    public static readonly Error ConexaoNaoIniciada = new(
        "merchant.conexao_nao_iniciada",
        "Nenhuma conexão com o iFood foi iniciada para esta loja.",
        ErrorType.Conflict);

    public static readonly Error CodigoExpirado = new(
        "merchant.codigo_expirado",
        "O código de autorização expirou. Inicie a conexão novamente.",
        ErrorType.Conflict);

    public static readonly Error JaConectado = new(
        "merchant.ja_conectado",
        "Esta loja já está conectada ao iFood.",
        ErrorType.Conflict);

    public static readonly Error TaxaInvalida = new(
        "merchant.taxa_invalida",
        "A taxa por entrega não pode ser negativa.",
        ErrorType.Validation);

    public static readonly Error AppShopIdInvalido = new(
        "merchant.app_shop_id_invalido",
        "Informe o identificador da loja cadastrado na 99Food.",
        ErrorType.Validation);
}
