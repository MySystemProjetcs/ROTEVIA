using DeliveryHub.Application.Dashboard;
using DeliveryHub.Domain.Couriers;
using DeliveryHub.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace DeliveryHub.Infrastructure.Persistence;

internal sealed class ResumoDashboardQuery : IObterResumoDashboard
{
    // "Dia" é dia operacional da loja (America/Sao_Paulo), não dia UTC: pedido
    // recebido às 23h de SP não pode cair no dia seguinte do painel.
    private static readonly TimeZoneInfo FusoLoja =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private static readonly StatusPedido[] EmEntrega =
    [
        StatusPedido.Despachado, StatusPedido.Aceito, StatusPedido.EmRota, StatusPedido.Chegou
    ];

    private readonly AppDbContext _db;

    public ResumoDashboardQuery(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ResumoDashboard> ExecutarAsync(Guid merchantId, CancellationToken ct)
    {
        var agoraSp = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, FusoLoja);

        // A virada do dia é a do fuso da loja — "hoje" para o lojista começa à
        // meia-noite em São Paulo, não em UTC. Mas o parâmetro vai para uma
        // coluna timestamptz, e o Npgsql só aceita offset zero: converter para
        // UTC mantém o mesmo instante e deixa a comparação correta.
        var inicioDia = new DateTimeOffset(agoraSp.Date, FusoLoja.GetUtcOffset(agoraSp.Date))
            .ToUniversalTime();
        var fimDia = inicioDia.AddDays(1);

        var taxa = await _db.Merchants
            .AsNoTracking()
            .Where(x => x.Id == merchantId)
            .Select(x => x.TaxaPadraoPorEntrega)
            .FirstOrDefaultAsync(ct);

        var online = await _db.CourierMerchantLinks
            .AsNoTracking()
            .Where(link => link.MerchantId == merchantId && link.Status == StatusVinculoEntregador.Ativo)
            .Join(_db.Couriers,
                link => link.CourierId, courier => courier.Id,
                (link, courier) => courier.DisponivelParaEntrega)
            .CountAsync(disponivel => disponivel, ct);

        var emEntrega = await _db.Pedidos
            .AsNoTracking()
            .Where(x => x.MerchantId == merchantId
                && x.EntregadorId != null
                && EmEntrega.Contains(x.Status))
            .Select(x => x.EntregadorId!.Value)
            .Distinct()
            .CountAsync(ct);

        var doDia = _db.Pedidos
            .AsNoTracking()
            .Where(x => x.MerchantId == merchantId
                && x.Status != StatusPedido.Cancelado
                && x.RecebidoEm >= inicioDia
                && x.RecebidoEm < fimDia);

        var qtd = await doDia.CountAsync(ct);

        // Receita é dinheiro que entrou: só pedido concluído e sem valor
        // pendente. Pedido em rota ainda pode ser cancelado, e pedido entregue
        // com pagamento na porta só vira receita quando o motoboy cobra.
        //
        // Esta é a diferença entre "Pedidos hoje" e "Receita hoje": o primeiro
        // é volume da operação, o segundo é caixa.
        var receita = await doDia
            .Where(x => x.Status == StatusPedido.Concluido && x.Pagamento.ValorACobrar == 0)
            .SumAsync(x => x.ValorTotal, ct);

        var qtdDeTeste = await doDia.CountAsync(x => x.EhTeste, ct);

        return new ResumoDashboard(online, emEntrega, qtd, receita, taxa, qtdDeTeste);
    }
}
