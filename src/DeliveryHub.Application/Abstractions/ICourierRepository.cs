using DeliveryHub.Domain.Couriers;

namespace DeliveryHub.Application.Abstractions;

// Projeção pronta pra tela de Motoboys — evita expor Courier+CourierMerchantLink
// separados e recompor o join na Application.
public sealed record EntregadorResumo(
    Guid LinkId, Guid CourierId, string Nome, string Telefone,
    string ModeloDaMoto, string Placa, StatusVinculoEntregador Status, bool Disponivel);

public interface ICourierRepository
{
    Task<Courier?> ObterPorCpfAsync(string cpf, CancellationToken ct);
    Task<Courier?> ObterPorIdAsync(Guid id, CancellationToken ct);

    // Resolve "que Courier é esse usuário logado" a partir do sub do JWT —
    // usado pelas ações do motoboy, que não carregam courier_id no token.
    Task<Courier?> ObterPorUsuarioIdAsync(Guid usuarioId, CancellationToken ct);
    void Adicionar(Courier courier);

    Task<CourierMerchantLink?> ObterLinkAsync(Guid linkId, CancellationToken ct);

    Task<bool> ExisteVinculoAtivoOuPendenteAsync(Guid courierId, Guid merchantId, CancellationToken ct);

    // Distinto do acima: aqui exige Ativo mesmo (não Convidado) — é a
    // checagem de "pode alocar esse motoboy nesse pedido".
    Task<bool> ExisteVinculoAtivoAsync(Guid courierId, Guid merchantId, CancellationToken ct);

    void AdicionarLink(CourierMerchantLink link);

    Task<IReadOnlyList<EntregadorResumo>> ListarPorMerchantAsync(Guid merchantId, CancellationToken ct);

    // Em quais lojas esse motoboy está Ativo agora — um Courier pode
    // trabalhar pra mais de um restaurante.
    Task<IReadOnlyList<Guid>> ListarMerchantIdsAtivosAsync(Guid courierId, CancellationToken ct);

    Task SalvarAsync(CancellationToken ct);
}
