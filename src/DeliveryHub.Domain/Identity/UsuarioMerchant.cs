namespace DeliveryHub.Domain.Identity;

// O vínculo entre usuário e loja. Existe separado porque um dono pode ter
// várias lojas: o tenant ativo vem do token, não do usuário.
public sealed class UsuarioMerchant
{
    private UsuarioMerchant() { }

    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public Guid MerchantId { get; private set; }
    public DateTimeOffset VinculadoEm { get; private set; }

    public static UsuarioMerchant Criar(Guid usuarioId, Guid merchantId, DateTimeOffset vinculadoEm) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UsuarioId = usuarioId,
            MerchantId = merchantId,
            VinculadoEm = vinculadoEm
        };
}
