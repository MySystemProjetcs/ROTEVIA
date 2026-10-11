using DeliveryHub.Domain.Orders;

namespace DeliveryHub.Application.Abstractions;

// Porta de saída pro push do painel: quem muda estado chama, quem entrega é
// a Api (SignalR). Falha aqui nunca pode quebrar o caso de uso — a
// implementação engole erro de transporte e só loga.
public interface INotificadorPainel
{
    Task ResumoAtualizadoAsync(Guid merchantId, CancellationToken ct);

    // Pedido novo pronto para imprimir: a estação do restaurante recebe o
    // payload da comanda e decide o destino (ESC/POS físico ou PDF em dev).
    Task PedidoParaImprimirAsync(Guid merchantId, PedidoReceiptSnapshot comanda, CancellationToken ct);
}
