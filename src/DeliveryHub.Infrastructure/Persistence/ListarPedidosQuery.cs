using DeliveryHub.Application.Orders;
using DeliveryHub.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace DeliveryHub.Infrastructure.Persistence;

internal sealed class ListarPedidosQuery : IListarPedidos
{
    // HashSet (não array nem collection-expression): no .NET 10, `T[].Contains`
    // e `[..].Contains` passam a resolver para MemoryExtensions.Contains
    // (ReadOnlySpan<T>,T), que o EF Core não consegue funcletizar e explode a
    // tradução da query. HashSet<T>.Contains é instância, resolve sem ambiguidade.
    private static readonly HashSet<StatusPedido> Ativos = new()
    {
        StatusPedido.Recebido,
        StatusPedido.Confirmado,
        StatusPedido.EmPreparo,
        StatusPedido.Pronto,
        StatusPedido.Despachado,
        StatusPedido.Aceito,
        StatusPedido.EmRota,
        StatusPedido.Chegou,
        StatusPedido.Cobrar
    };

    // Mesmo fuso do resumo do painel: "hoje" para o lojista começa à meia-noite
    // de São Paulo, não em UTC.
    private static readonly TimeZoneInfo FusoLoja =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private readonly AppDbContext _db;

    public ListarPedidosQuery(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PedidoDto>> ExecutarAsync(bool apenasAtivos, CancellationToken ct)
    {
        // Leitura projetada direto para DTO, sem tracking: carregar a entidade
        // inteira para depois mapear é desperdício (ENGINEERING-GUIDE §6).
        var consulta = _db.Pedidos.AsNoTracking();

        if (apenasAtivos)
        {
            // O painel também mostra a coluna "Finalizados", mas só a do dia: o
            // concluído de ontem não é operação de hoje, e trazer o histórico
            // inteiro faria a listagem crescer sem teto — ela roda a cada 4s.
            var agoraNaLoja = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, FusoLoja);
            var inicioDoDia = new DateTimeOffset(
                    agoraNaLoja.Date,
                    FusoLoja.GetUtcOffset(agoraNaLoja.Date))
                .ToUniversalTime();

            consulta = consulta.Where(x =>
                Ativos.Contains(x.Status)
                || (x.Status == StatusPedido.Concluido && x.RecebidoEm >= inicioDoDia));
        }

        return await consulta
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
                x.EntregadorId == null
                    ? null
                    : _db.Couriers.Where(c => c.Id == x.EntregadorId).Select(c => c.Nome).FirstOrDefault(),
                // O dono já sabe de que loja é o pedido: só a listagem do
                // motoboy preenche isso.
                null,
                // "local-" é o único prefixo que o próprio sistema gera
                // (Pedido.PrefixoOrigemLocal); qualquer outra coisa hoje só
                // pode ser o iFood, a única origem externa em produção.
                x.IdExterno.StartsWith("local-") ? OrigemDoPedido.Interno : OrigemDoPedido.IFood,
                x.ExigeCodigoDeEntrega,
                x.CodigoConfirmadoEm,
                x.LoteEntregaId,
                x.OrdemNaRota,
                x.CodigoDeEntrega))
            .ToListAsync(ct);
    }
}
