using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Domain.Couriers;

public enum StatusVinculoEntregador
{
    Convidado = 0,
    Ativo = 1,
    Suspenso = 2
}

// O vínculo entre um Courier (global) e uma loja. O convite pendente mora nos
// próprios campos do vínculo — sem tabela separada — no mesmo espírito do
// ConexaoIFood: os campos de convite ficam nulos assim que ele é confirmado.
public sealed class CourierMerchantLink : ITenantOwned
{
    private CourierMerchantLink() { }

    public Guid Id { get; private set; }
    public Guid CourierId { get; private set; }
    public Guid MerchantId { get; private set; }
    public StatusVinculoEntregador Status { get; private set; }

    // Login pretendido pelo restaurante no convite. Só é usado para criar o
    // Usuario se o Courier ainda não tiver um (Courier.UsuarioId nulo).
    public string Email { get; private set; } = string.Empty;

    // Hash, nunca o token em claro — comparado via IPasswordHasher.Verificar no
    // Application, igual a uma senha. Nulo depois que o vínculo vira Ativo.
    public string? TokenConviteHash { get; private set; }
    public DateTimeOffset? ConviteExpiraEm { get; private set; }

    public DateTimeOffset VinculadoEm { get; private set; }
    public DateTimeOffset? AtivadoEm { get; private set; }

    public static CourierMerchantLink Convidar(
        Guid courierId, Guid merchantId, string email, string tokenConviteHash,
        DateTimeOffset conviteExpiraEm, DateTimeOffset agora) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            CourierId = courierId,
            MerchantId = merchantId,
            Status = StatusVinculoEntregador.Convidado,
            Email = email,
            TokenConviteHash = tokenConviteHash,
            ConviteExpiraEm = conviteExpiraEm,
            VinculadoEm = agora
        };

    public Result Ativar(DateTimeOffset agora)
    {
        if (Status != StatusVinculoEntregador.Convidado)
            return Result.Failure(CourierErrors.ConviteInvalido);

        if (ConviteExpiraEm is null || ConviteExpiraEm < agora)
            return Result.Failure(CourierErrors.ConviteExpirado);

        Status = StatusVinculoEntregador.Ativo;
        AtivadoEm = agora;
        TokenConviteHash = null;
        ConviteExpiraEm = null;

        return Result.Success();
    }
}
