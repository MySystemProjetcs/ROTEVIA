namespace DeliveryHub.Domain.Merchants;

// O tenant. É por IFoodMerchantId que a ingestão traduz o merchantId que vem
// no evento do iFood para o nosso MerchantId — sem isso o pedido não tem dono.
public sealed class Merchant
{
    // O nome do parâmetro precisa bater com a propriedade na convenção do EF
    // Core (só a primeira letra minúscula), senão a materialização falha.
    private Merchant(Guid id, string nome, Guid iFoodMerchantId, DateTimeOffset criadoEm)
    {
        Id = id;
        Nome = nome;
        IFoodMerchantId = iFoodMerchantId;
        CriadoEm = criadoEm;
    }

    public Guid Id { get; }
    public string Nome { get; private set; }
    public Guid IFoodMerchantId { get; }
    public DateTimeOffset CriadoEm { get; }

    public static Merchant Criar(string nome, Guid ifoodMerchantId, DateTimeOffset criadoEm) =>
        new(Guid.CreateVersion7(), nome, ifoodMerchantId, criadoEm);
}
