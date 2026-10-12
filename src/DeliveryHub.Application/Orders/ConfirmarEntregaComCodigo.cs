using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Orders;
using DeliveryHub.Domain.SharedKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeliveryHub.Application.Orders;

public interface IConfirmarEntregaComCodigo
{
    Task<Result> ExecutarAsync(
        Guid usuarioLogadoId, Guid pedidoId, string codigo, CancellationToken ct);
}

// O código de 4 dígitos que o cliente informa na porta. Caso de uso próprio e
// não mais um item do AcaoDeEntrega: aquela enum passa por um ExecutarAsync sem
// payload, e enfiar um `string?` lá contaminaria as cinco ações que não têm
// código.
public sealed class ConfirmarEntregaComCodigo : IConfirmarEntregaComCodigo
{
    private readonly IPedidoRepository _pedidos;
    private readonly ICourierRepository _couriers;
    private readonly IOrderSource _origem;
    private readonly TimeProvider _relogio;
    private readonly ILogger<ConfirmarEntregaComCodigo> _logger;

    public ConfirmarEntregaComCodigo(
        IPedidoRepository pedidos,
        ICourierRepository couriers,
        IOrderSource origem,
        TimeProvider relogio,
        ILogger<ConfirmarEntregaComCodigo>? logger = null)
    {
        _pedidos = pedidos;
        _couriers = couriers;
        _origem = origem;
        _relogio = relogio;
        _logger = logger ?? NullLogger<ConfirmarEntregaComCodigo>.Instance;
    }

    public async Task<Result> ExecutarAsync(
        Guid usuarioLogadoId, Guid pedidoId, string codigo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            _logger.LogWarning("Confirmação de entrega recusada: código ausente. PedidoId={PedidoId}", pedidoId);
            return Result.Failure(PedidoErrors.CodigoDeEntregaInvalido);
        }

        var courier = await _couriers.ObterPorUsuarioIdAsync(usuarioLogadoId, ct);
        if (courier is null)
        {
            _logger.LogWarning("Confirmação de entrega recusada: usuário sem entregador associado. PedidoId={PedidoId}", pedidoId);
            return Result.Failure(PedidoErrors.EntregadorNaoPertenceAoPedido);
        }

        var pedido = await _pedidos.ObterParaEntregadorAsync(pedidoId, ct);

        // Mesma mensagem pra "pedido não existe" e "não é seu": não revela
        // que o pedido existe pra quem não foi designado pra ele.
        if (pedido is null || pedido.EntregadorId != courier.Id)
        {
            _logger.LogWarning("Confirmação de entrega recusada: pedido não atribuído ao entregador. PedidoId={PedidoId} EntregadorId={EntregadorId}",
                pedidoId, courier.Id);
            return Result.Failure(PedidoErrors.EntregadorNaoPertenceAoPedido);
        }

        // Já confirmado: sucesso silencioso. O motoboy tocou duas vezes, ou a
        // resposta da primeira se perdeu na rua.
        if (!pedido.CodigoDeEntregaPendente)
        {
            _logger.LogInformation("Confirmação de entrega idempotente: código já confirmado. PedidoId={PedidoId} MerchantId={MerchantId}",
                pedido.Id, pedido.MerchantId);
            return Result.Success();
        }

        // Origem primeiro, domínio depois — mesma ordem do AvancarPedido: só
        // gravamos a confirmação depois que o iFood aceitou o código.
        var verificacao = await _origem.VerificarCodigoDeEntregaAsync(
            pedido.IdExterno, codigo.Trim(), ct);

        if (verificacao.IsFailure)
        {
            _logger.LogWarning("Origem recusou validação do código de entrega. PedidoId={PedidoId} MerchantId={MerchantId} Erro={Erro}",
                pedido.Id, pedido.MerchantId, verificacao.Error.Code);
            return Result.Failure(verificacao.Error);
        }

        // Código errado é falha de negócio, não erro: o motoboy confere com o
        // cliente e digita de novo.
        if (!verificacao.Value)
        {
            _logger.LogWarning("Código de entrega inválido. PedidoId={PedidoId} MerchantId={MerchantId} EntregadorId={EntregadorId}",
                pedido.Id, pedido.MerchantId, courier.Id);
            return Result.Failure(PedidoErrors.CodigoDeEntregaInvalido);
        }

        pedido.RegistrarCodigoConfirmado(_relogio.GetUtcNow());
        await _pedidos.SalvarAsync(ct);

        _logger.LogInformation("Código de entrega confirmado. PedidoId={PedidoId} MerchantId={MerchantId} EntregadorId={EntregadorId}",
            pedido.Id, pedido.MerchantId, courier.Id);
        return Result.Success();
    }
}
