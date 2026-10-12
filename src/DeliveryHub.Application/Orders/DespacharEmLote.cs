using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Couriers;
using DeliveryHub.Domain.Merchants;
using DeliveryHub.Domain.Orders;
using DeliveryHub.Domain.SharedKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;

namespace DeliveryHub.Application.Orders;

public static class DespacharEmLoteErrors
{
    public static readonly Error LoteMuitoPequeno = new(
        "lote.muito_pequeno",
        "Selecione ao menos dois pedidos para despachar juntos.",
        ErrorType.Validation);

    public static readonly Error PedidoNaoDespachavel = new(
        "lote.pedido_nao_despachavel",
        "Todos os pedidos do lote precisam estar Prontos para despachar.",
        ErrorType.Conflict);

    public static readonly Error TenantAusente = new(
        "lote.tenant_ausente",
        "Sessão sem loja definida.",
        ErrorType.Unauthorized);
}

public interface IDespacharEmLote
{
    Task<Result> ExecutarAsync(Guid entregadorId, IReadOnlyList<Guid> pedidoIds, CancellationToken ct);
}

// Pedidos casados: N pedidos saem juntos com o mesmo motoboy, numa corrida só.
// O sistema calcula a ordem das paradas (vizinho-mais-próximo a partir da loja)
// e despacha todos. Mesma disciplina do AvancarPedido: avisa a origem primeiro,
// depois move o domínio — mas em lote, sob um LoteEntregaId comum.
public sealed class DespacharEmLote : IDespacharEmLote
{
    private readonly IPedidoRepository _pedidos;
    private readonly ICourierRepository _couriers;
    private readonly IMerchantRepository _merchants;
    private readonly IOrderSource _origem;
    private readonly INotificadorPainel _notificador;
    private readonly ITenantContext _tenant;
    private readonly ILogger<DespacharEmLote> _logger;

    public DespacharEmLote(
        IPedidoRepository pedidos,
        ICourierRepository couriers,
        IMerchantRepository merchants,
        IOrderSource origem,
        INotificadorPainel notificador,
        ITenantContext tenant,
        ILogger<DespacharEmLote>? logger = null)
    {
        _pedidos = pedidos;
        _couriers = couriers;
        _merchants = merchants;
        _origem = origem;
        _notificador = notificador;
        _tenant = tenant;
        _logger = logger ?? NullLogger<DespacharEmLote>.Instance;
    }

    public async Task<Result> ExecutarAsync(
        Guid entregadorId, IReadOnlyList<Guid> pedidoIds, CancellationToken ct)
    {
        var duracao = Stopwatch.StartNew();
        var ids = pedidoIds.Distinct().ToList();
        if (ids.Count < 2)
        {
            _logger.LogWarning("Despacho em lote recusado: quantidade insuficiente. Quantidade={Quantidade}", ids.Count);
            return Result.Failure(DespacharEmLoteErrors.LoteMuitoPequeno);
        }

        if (_tenant.MerchantId is not { } merchantId)
        {
            _logger.LogWarning("Despacho em lote recusado: tenant ausente. Quantidade={Quantidade}", ids.Count);
            return Result.Failure(DespacharEmLoteErrors.TenantAusente);
        }

        _logger.LogInformation("Iniciando despacho em lote. MerchantId={MerchantId} EntregadorId={EntregadorId} Quantidade={Quantidade}",
            merchantId, entregadorId, ids.Count);

        // Carrega e valida todos antes de qualquer efeito colateral.
        var pedidos = new List<Pedido>(ids.Count);
        foreach (var id in ids)
        {
            var pedido = await _pedidos.ObterPorIdAsync(id, ct);

            // Mesma mensagem para "não existe" e "não é desta loja": não revela
            // pedido de outro tenant.
            if (pedido is null || pedido.MerchantId != merchantId)
            {
                _logger.LogWarning("Despacho em lote recusado: pedido não encontrado no tenant. MerchantId={MerchantId} Quantidade={Quantidade}",
                    merchantId, ids.Count);
                return Result.Failure(AvancarPedidoErrors.PedidoNaoEncontrado);
            }

            if (pedido.Status != StatusPedido.Pronto)
            {
                _logger.LogWarning("Despacho em lote recusado: estado incompatível. PedidoId={PedidoId} MerchantId={MerchantId} Status={Status} Erro={Erro}",
                    pedido.Id, merchantId, pedido.Status, DespacharEmLoteErrors.PedidoNaoDespachavel.Code);
                return Result.Failure(DespacharEmLoteErrors.PedidoNaoDespachavel);
            }

            pedidos.Add(pedido);
        }

        if (!await _couriers.ExisteVinculoAtivoAsync(entregadorId, merchantId, ct))
        {
            _logger.LogWarning("Despacho em lote recusado: vínculo de entregador inativo ou ausente. MerchantId={MerchantId} EntregadorId={EntregadorId}",
                merchantId, entregadorId);
            return Result.Failure(CourierErrors.VinculoNaoEncontrado);
        }

        var loja = await _merchants.ObterPorIdAsync(merchantId, ct);
        var ordem = CalcularOrdem(pedidos, loja);
        var otimizouRota = loja?.Endereco is not null;

        // Origem primeiro (efeito colateral no marketplace), só então o domínio.
        // Falha de origem aborta o lote inteiro: os pedidos que já foram
        // aceitos lá fora são reconciliados pelo evento DISPATCHED do polling.
        var porId = pedidos.ToDictionary(p => p.Id);
        foreach (var id in ordem)
        {
            var pedido = porId[id];
            if (pedido.TemOrigemExterna)
            {
                var naOrigem = await _origem.DespacharAsync(pedido.IdExterno, ct);
                if (naOrigem.IsFailure)
                {
                    _logger.LogWarning("Origem recusou despacho em lote. PedidoId={PedidoId} MerchantId={MerchantId} Erro={Erro} Quantidade={Quantidade} DuraçãoMs={DuracaoMs}",
                        pedido.Id, merchantId, naOrigem.Error.Code, ids.Count, duracao.Elapsed.TotalMilliseconds);
                    return naOrigem;
                }
            }
        }

        var loteId = Guid.NewGuid();
        for (var i = 0; i < ordem.Count; i++)
        {
            var pedido = porId[ordem[i]];
            pedido.AlocarEntregador(entregadorId);
            pedido.DefinirRotaDeLote(loteId, i + 1);

            var despacho = pedido.Despachar();
            if (despacho.IsFailure)
            {
                _logger.LogWarning("Domínio recusou item do despacho em lote. PedidoId={PedidoId} MerchantId={MerchantId} Erro={Erro}",
                    pedido.Id, merchantId, despacho.Error.Code);
                return despacho;
            }
        }

        await _pedidos.SalvarAsync(ct);
        await _notificador.ResumoAtualizadoAsync(merchantId, ct);
        _logger.LogInformation("Lote de pedidos despachado. LoteId={LoteId} MerchantId={MerchantId} EntregadorId={EntregadorId} Quantidade={Quantidade} OtimizouRota={OtimizouRota} DuraçãoMs={DuracaoMs}",
            loteId, merchantId, entregadorId, ids.Count, otimizouRota, duracao.Elapsed.TotalMilliseconds);
        return Result.Success();
    }

    private static IReadOnlyList<Guid> CalcularOrdem(IReadOnlyList<Pedido> pedidos, Merchant? loja)
    {
        var origem = loja?.Endereco;

        // Sem coordenada da loja não há de onde partir: mantém a ordem de
        // seleção. Melhor um lote sem otimização do que nenhum lote.
        if (origem is null)
            return pedidos.Select(p => p.Id).ToList();

        var paradas = pedidos
            .Select(p => new ParadaDaRota(p.Id, CoordValida(p.EnderecoEntrega?.Latitude), CoordValida(p.EnderecoEntrega?.Longitude)))
            .ToList();

        return SequenciadorDeRota.Ordenar(origem.Latitude, origem.Longitude, paradas);
    }

    // 0,0 é "sem coordenada" disfarçado: o sandbox do iFood manda 0,0 e o
    // ponto cairia no meio do Atlântico, distorcendo a sequência.
    private static double? CoordValida(double? valor) =>
        valor is null or 0d ? null : valor;
}
