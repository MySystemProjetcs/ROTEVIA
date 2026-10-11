using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Orders;
using DeliveryHub.Domain.SharedKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeliveryHub.Application.Orders;

public enum AcaoDeEntrega
{
    Aceitar,
    SairParaEntrega,
    ChegarNoLocal,
    Cobrar,
    Finalizar
}

public interface IAvancarEntrega
{
    Task<Result> ExecutarAsync(Guid usuarioLogadoId, Guid pedidoId, AcaoDeEntrega acao, CancellationToken ct);
}

// Espelha o AvancarPedido, mas do lado do motoboy: nenhuma dessas ações avisa
// o iFood (só o Despachar do dono faz isso) — é status só nosso.
public sealed class AvancarEntrega : IAvancarEntrega
{
    private readonly IPedidoRepository _pedidos;
    private readonly ICourierRepository _couriers;
    private readonly IMerchantRepository _merchants;
    private readonly INotificadorPainel _notificador;
    private readonly ILogger<AvancarEntrega> _logger;

    public AvancarEntrega(
        IPedidoRepository pedidos, ICourierRepository couriers,
        IMerchantRepository merchants, INotificadorPainel notificador,
        ILogger<AvancarEntrega>? logger = null)
    {
        _pedidos = pedidos;
        _couriers = couriers;
        _merchants = merchants;
        _notificador = notificador;
        _logger = logger ?? NullLogger<AvancarEntrega>.Instance;
    }

    public async Task<Result> ExecutarAsync(Guid usuarioLogadoId, Guid pedidoId, AcaoDeEntrega acao, CancellationToken ct)
    {
        var courier = await _couriers.ObterPorUsuarioIdAsync(usuarioLogadoId, ct);
        if (courier is null)
        {
            _logger.LogWarning("Ação de entrega recusada: usuário sem entregador associado. PedidoId={PedidoId} Ação={Acao}", pedidoId, acao);
            return Result.Failure(PedidoErrors.EntregadorNaoPertenceAoPedido);
        }

        var pedido = await _pedidos.ObterParaEntregadorAsync(pedidoId, ct);

        // Mesma mensagem pra "pedido não existe" e "não é seu": não revela
        // que o pedido existe pra quem não foi designado pra ele.
        if (pedido is null || pedido.EntregadorId != courier.Id)
        {
            _logger.LogWarning("Ação de entrega recusada: pedido não atribuído ao entregador. PedidoId={PedidoId} EntregadorId={EntregadorId} Ação={Acao}",
                pedidoId, courier.Id, acao);
            return Result.Failure(PedidoErrors.EntregadorNaoPertenceAoPedido);
        }

        var statusAnterior = pedido.Status;

        var resultado = acao switch
        {
            AcaoDeEntrega.Aceitar => pedido.AceitarEntrega(),
            AcaoDeEntrega.SairParaEntrega => pedido.SairParaEntrega(),
            AcaoDeEntrega.ChegarNoLocal => pedido.ChegarNoLocal(),
            AcaoDeEntrega.Cobrar => pedido.Cobrar(),
            // Pela porta do entregador, que recusa enquanto o código do
            // cliente não foi confirmado. O Concluir() cru continua valendo
            // para o evento CONCLUDED do iFood.
            AcaoDeEntrega.Finalizar => pedido.ConcluirPeloEntregador(),
            _ => throw new ArgumentOutOfRangeException(nameof(acao)),
        };

        if (resultado.IsFailure)
        {
            _logger.LogWarning("Domínio recusou ação de entrega. PedidoId={PedidoId} MerchantId={MerchantId} EntregadorId={EntregadorId} Ação={Acao} Status={Status} Erro={Erro}",
                pedido.Id, pedido.MerchantId, courier.Id, acao, statusAnterior, resultado.Error.Code);
            return resultado;
        }

        // Fato gerador do ganho: finalizar grava o snapshot da taxa vigente.
        // Sem entregador não gera repasse (fica nulo).
        //
        // Pedido de teste também gera, por decisão de produto: o sandbox do
        // iFood marca tudo como teste, e excluir daqui deixava o relatório do
        // motoboy permanentemente vazio. O que impede teste de virar dinheiro
        // de verdade é o Ledger (CLAUDE.md §7), não esta leitura.
        if (acao == AcaoDeEntrega.Finalizar
            && pedido.EntregadorId is not null
            && !pedido.ValorPagoAoEntregador.HasValue)
        {
            var merchant = await _merchants.ObterPorIdAsync(pedido.MerchantId, ct);
            if (merchant is not null)
            {
                var repasse = pedido.RegistrarRepasseAoEntregador(merchant.TaxaPadraoPorEntrega);
                if (repasse.IsFailure)
                {
                    _logger.LogWarning("Registro de repasse recusado. PedidoId={PedidoId} MerchantId={MerchantId} Erro={Erro}",
                        pedido.Id, pedido.MerchantId, repasse.Error.Code);
                    return repasse;
                }
            }
        }

        await _pedidos.SalvarAsync(ct);
        await _notificador.ResumoAtualizadoAsync(pedido.MerchantId, ct);
        _logger.LogInformation("Ação de entrega concluída. PedidoId={PedidoId} MerchantId={MerchantId} EntregadorId={EntregadorId} Ação={Acao} StatusAnterior={StatusAnterior} StatusNovo={StatusNovo} RepasseRegistrado={RepasseRegistrado}",
            pedido.Id, pedido.MerchantId, courier.Id, acao, statusAnterior, pedido.Status, pedido.ValorPagoAoEntregador.HasValue);
        return Result.Success();
    }
}
