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

    public Task SalvarAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
