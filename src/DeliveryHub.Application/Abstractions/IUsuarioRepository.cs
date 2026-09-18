using DeliveryHub.Domain.Identity;

namespace DeliveryHub.Application.Abstractions;

public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken ct);
    Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken ct);
    Task<bool> ExisteComEmailAsync(string email, CancellationToken ct);
    Task<Guid?> ObterMerchantVinculadoAsync(Guid usuarioId, CancellationToken ct);

    void Adicionar(Usuario usuario);
    void Vincular(UsuarioMerchant vinculo);
    Task SalvarAsync(CancellationToken ct);
}
