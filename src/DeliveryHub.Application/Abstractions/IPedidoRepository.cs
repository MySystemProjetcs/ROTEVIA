using DeliveryHub.Domain.Orders;

namespace DeliveryHub.Application.Abstractions;

public interface IPedidoRepository
{
    Task<Pedido?> ObterPorIdAsync(Guid id, CancellationToken ct);

    // Sem filtro de tenant: o motoboy não tem merchant_id no token (pode
    // atender mais de um restaurante), então o filtro global bloquearia
    // qualquer leitura. A autorização é manual — quem chama confere
    // pedido.EntregadorId contra o Courier do usuário logado.
    Task<Pedido?> ObterParaEntregadorAsync(Guid id, CancellationToken ct);

    // A entrega que o motoboy está fazendo agora, se houver. O ping de GPS não
    // sabe o id do pedido — quem sabe é o servidor.
    Task<Pedido?> ObterEntregaEmCursoAsync(Guid entregadorId, CancellationToken ct);
    // Guarda da remoção de vínculo: tirar o motoboy da loja no meio de uma
    // entrega deixaria o pedido sem responsável.
    Task<bool> TemEntregaAtivaAsync(Guid entregadorId, Guid merchantId, CancellationToken ct);

    void Adicionar(Pedido pedido);

    Task SalvarAsync(CancellationToken ct);
}
