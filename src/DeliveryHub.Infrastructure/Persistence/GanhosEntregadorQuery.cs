using DeliveryHub.Application.Couriers;
using DeliveryHub.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace DeliveryHub.Infrastructure.Persistence;

internal sealed class GanhosEntregadorQuery : IObterGanhosEntregador
{
    private static readonly TimeZoneInfo FusoLoja =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private readonly AppDbContext _db;

    public GanhosEntregadorQuery(AppDbContext db)
    {
        _db = db;
    }

    // Cabe numa tela sem rolagem longa e mantém a resposta pequena o bastante
    // para o 3G do motoboy.
    private const int TamanhoPagina = 20;

    public async Task<ResultadoGanhos> ExecutarAsync(
        Guid usuarioId,
        IntervaloDeGanhos intervalo,
        int pagina,
        CancellationToken ct)
    {
        // Página fora do intervalo vira a primeira: é query string, qualquer um
        // digita o que quiser ali.
        if (pagina < 1) pagina = 1;

        var courierId = await _db.Couriers
            .AsNoTracking()
            .Where(x => x.UsuarioId == usuarioId)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(ct);

        if (courierId == Guid.Empty)
            return new ResultadoGanhos(0, 0, [], pagina, TamanhoPagina);

        // As datas vêm como dia no fuso da loja. O fim é exclusivo — somar um
        // dia em vez de usar 23:59:59 inclui a entrega feita às 23h59m30 e não
        // depende da precisão do timestamp.
        var inicioIntervalo = InstanteEmSaoPaulo(intervalo.Inicio);
        var fimIntervalo = InstanteEmSaoPaulo(intervalo.Fim.AddDays(1));

        // Só entrega concluída com repasse gravado conta como ganho — e quem
        // decide se houve repasse é o momento da conclusão, não esta consulta.
        // Sem filtro de tenant de propósito, igual à ListarMinhasEntregasQuery:
        // o motoboy não tem merchant_id no token e o filtro global zeraria a
        // lista. A autorização é o EntregadorId resolvido do sub do JWT.
        var doPeriodo = _db.Pedidos
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.EntregadorId == courierId
                && x.Status == StatusPedido.Concluido
                && x.ValorPagoAoEntregador != null
                && x.RecebidoEm >= inicioIntervalo
                && x.RecebidoEm < fimIntervalo);

        // Agregados no banco, sobre o período todo — somar em memória a partir
        // da página daria o total da tela, não o do mês.
        var qtd = await doPeriodo.CountAsync(ct);
        var total = await doPeriodo.SumAsync(x => x.ValorPagoAoEntregador!.Value, ct);

        var itens = await doPeriodo
            // Subconsulta em vez de Join: o Join com Merchants não é traduzível
            // aqui (o EF acaba boxando as chaves para object e desiste). É o
            // mesmo caminho que a ListarMinhasEntregasQuery já usa para o nome
            // da loja, e esse traduz.
            .OrderByDescending(x => x.RecebidoEm)
            // Skip/Take depois do OrderBy: sem ordem estável a página 2 pode
            // repetir ou pular linha que já apareceu na 1.
            .Skip((pagina - 1) * TamanhoPagina)
            .Take(TamanhoPagina)
            .Select(pedido => new ItemGanho(
                pedido.Id,
                pedido.NumeroExibicao,
                _db.Merchants.Where(m => m.Id == pedido.MerchantId).Select(m => m.Nome).FirstOrDefault() ?? string.Empty,
                pedido.ValorPagoAoEntregador!.Value,
                pedido.RecebidoEm,
                pedido.Cliente.Nome,
                pedido.EnderecoEntrega == null
                    ? null
                    : pedido.EnderecoEntrega.Logradouro + ", " + pedido.EnderecoEntrega.Numero + " - " + pedido.EnderecoEntrega.Bairro,
                pedido.Itens
                    .OrderBy(i => i.Indice)
                    .Select(i => i.Quantidade + "× " + i.Nome)
                    .ToList()))
            .ToListAsync(ct);

        return new ResultadoGanhos(total, qtd, itens, pagina, TamanhoPagina);
    }

    // Meia-noite daquele dia em São Paulo, convertida para UTC: a coluna é
    // timestamptz e o Npgsql recusa parâmetro com offset diferente de zero.
    private static DateTimeOffset InstanteEmSaoPaulo(DateOnly dia)
    {
        var local = dia.ToDateTime(TimeOnly.MinValue);

        return new DateTimeOffset(local, FusoLoja.GetUtcOffset(local)).ToUniversalTime();
    }
}
