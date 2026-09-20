using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Orders;
using DeliveryHub.Domain.SharedKernel;

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

    public AvancarEntrega(
        IPedidoRepository pedidos, ICourierRepository couriers,
        IMerchantRepository merchants, INotificadorPainel notificador)
    {
        _pedidos = pedidos;
        _couriers = couriers;
        _merchants = merchants;
        _notificador = notificador;
    }

    public async Task<Result> ExecutarAsync(Guid usuarioLogadoId, Guid pedidoId, AcaoDeEntrega acao, CancellationToken ct)
    {
        var courier = await _couriers.ObterPorUsuarioIdAsync(usuarioLogadoId, ct);
        if (courier is null)
            return Result.Failure(PedidoErrors.EntregadorNaoPertenceAoPedido);

        var pedido = await _pedidos.ObterParaEntregadorAsync(pedidoId, ct);

        // Mesma mensagem pra "pedido não existe" e "não é seu": não revela
        // que o pedido existe pra quem não foi designado pra ele.
        if (pedido is null || pedido.EntregadorId != courier.Id)
            return Result.Failure(PedidoErrors.EntregadorNaoPertenceAoPedido);

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
            return resultado;

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
                    return repasse;
            }
        }

        await _pedidos.SalvarAsync(ct);
        await _notificador.ResumoAtualizadoAsync(pedido.MerchantId, ct);
        return Result.Success();
    }
}
