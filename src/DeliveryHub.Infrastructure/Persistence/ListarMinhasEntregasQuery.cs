using DeliveryHub.Application.Orders;
using DeliveryHub.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace DeliveryHub.Infrastructure.Persistence;

internal sealed class ListarMinhasEntregasQuery : IListarMinhasEntregas
{
    private static readonly StatusPedido[] StatusDeEntregaAtiva =
    [
        StatusPedido.Despachado,
        StatusPedido.Aceito,
        StatusPedido.EmRota,
        StatusPedido.Chegou,
        StatusPedido.Cobrar
    ];

    private readonly AppDbContext _db;

    public ListarMinhasEntregasQuery(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PedidoDto>> ExecutarAsync(Guid usuarioLogadoId, CancellationToken ct)
    {
        var courier = await _db.Couriers.AsNoTracking().FirstOrDefaultAsync(c => c.UsuarioId == usuarioLogadoId, ct);
        if (courier is null)
            return [];

        // Cross-tenant de propósito: o motoboy não tem merchant_id no token,
        // e pode legitimamente atender mais de um restaurante.
        return await _db.Pedidos
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.EntregadorId == courier.Id && StatusDeEntregaAtiva.Contains(x.Status))
            .OrderByDescending(x => x.RecebidoEm)
            .Select(x => new PedidoDto(
                x.Id,
                x.NumeroExibicao,
                x.Status,
                x.EhTeste,
                x.ValorTotal,
                x.TaxaEntrega,
                x.Cliente.Nome,
                x.EnderecoEntrega == null
                    ? null
                    : x.EnderecoEntrega.Logradouro + ", " + x.EnderecoEntrega.Numero + " - " + x.EnderecoEntrega.Bairro,
                // 0,0 é "desconhecido" na prática: é o que o sandbox do iFood
                // manda, e navegar para lá jogaria o motoboy no Atlântico.
                x.EnderecoEntrega == null || x.EnderecoEntrega.Latitude == 0
                    ? null
                    : (double?)x.EnderecoEntrega.Latitude,
                x.EnderecoEntrega == null || x.EnderecoEntrega.Longitude == 0
                    ? null
                    : (double?)x.EnderecoEntrega.Longitude,
                x.Pagamento.Descricao,
                x.Pagamento.ValorACobrar,
                x.CriadoNaOrigemEm,
                x.RecebidoEm,
                x.Itens
                    .OrderBy(i => i.Indice)
                    .Select(i => new ItemDoPedidoDto(
                        i.Indice, i.Nome, i.Quantidade, i.Unidade, i.PrecoUnitario, i.PrecoTotal, i.Observacoes))
                    .ToList(),
                x.EntregadorId,
                courier.Nome,
                _db.Merchants.Where(m => m.Id == x.MerchantId).Select(m => m.Nome).FirstOrDefault(),
                x.IdExterno.StartsWith("local-") ? OrigemDoPedido.Interno : OrigemDoPedido.IFood,
                x.ExigeCodigoDeEntrega,
                x.CodigoConfirmadoEm,
                x.LoteEntregaId,
                x.OrdemNaRota,
                x.CodigoDeEntrega))
            .ToListAsync(ct);
    }
}
