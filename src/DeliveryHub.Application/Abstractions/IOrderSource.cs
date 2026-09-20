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

    // Valida na origem o código que o cliente informa ao entregador na porta.
    // Devolve bool, não tipo da origem: Application não pode enxergar contrato
    // de marketplace (teste de arquitetura quebra o build se vazar).
    Task<Result<bool>> VerificarCodigoDeEntregaAsync(
        string idExternoPedido, string codigo, CancellationToken ct);
}
