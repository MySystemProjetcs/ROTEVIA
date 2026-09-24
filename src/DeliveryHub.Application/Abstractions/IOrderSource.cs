using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Application.Abstractions;

// Porta (hexagonal): origem de pedido — iFood hoje, 99Food/Anota AI/WhatsApp/
// PDV próprio depois (CLAUDE.md §5). O núcleo fala com esta interface e nunca
// sabe qual marketplace está do outro lado.
public interface IOrderSource
{
    Task<Result> ConfirmarPedidoAsync(string idExternoPedido, CancellationToken ct);
    Task<Result> IniciarPreparoAsync(string idExternoPedido, CancellationToken ct);
    Task<Result> MarcarProntoAsync(string idExternoPedido, CancellationToken ct);
    Task<Result> DespacharAsync(string idExternoPedido, CancellationToken ct);

    // Cancela o pedido na origem. O motivo vem em texto livre do lojista; cada
    // adapter traduz para o código de cancelamento que o seu marketplace exige
    // (o iFood pede um cancellationCode). Application nunca vê esse código.
    Task<Result> CancelarPedidoAsync(string idExternoPedido, string motivo, CancellationToken ct);

    // Valida na origem o código que o cliente informa ao entregador na porta.
    // Devolve bool, não tipo da origem: Application não pode enxergar contrato
    // de marketplace (teste de arquitetura quebra o build se vazar).
    Task<Result<bool>> VerificarCodigoDeEntregaAsync(
        string idExternoPedido, string codigo, CancellationToken ct);
}
