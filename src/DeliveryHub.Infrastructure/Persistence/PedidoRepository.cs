using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace DeliveryHub.Infrastructure.Persistence;

internal sealed class PedidoRepository : IPedidoRepository
{
    private readonly AppDbContext _db;

    public PedidoRepository(AppDbContext db)
    {
        _db = db;
    }

    // Com tracking de propósito: quem chama vai mudar o estado do pedido.
    public Task<Pedido?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        _db.Pedidos.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<Pedido?> ObterParaEntregadorAsync(Guid id, CancellationToken ct) =>
        _db.Pedidos.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id, ct);

    // Sem filtro de tenant pelo mesmo motivo do ObterParaEntregadorAsync: quem
    // chama é o motoboy, que não tem merchant_id no token.
    public Task<Pedido?> ObterEntregaEmCursoAsync(Guid entregadorId, CancellationToken ct) =>
        _db.Pedidos
            .IgnoreQueryFilters()
            .Where(x => x.EntregadorId == entregadorId
                && (x.Status == StatusPedido.EmRota || x.Status == StatusPedido.Chegou))
            .OrderByDescending(x => x.RecebidoEm)
            .FirstOrDefaultAsync(ct);

    public void Adicionar(Pedido pedido) => _db.Pedidos.Add(pedido);

    public Task SalvarAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
