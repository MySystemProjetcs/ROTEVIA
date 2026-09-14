namespace DeliveryHub.Application.Abstractions;

// Porta de saída pro push do painel: quem muda estado chama, quem entrega é
// a Api (SignalR). Falha aqui nunca pode quebrar o caso de uso — a
// implementação engole erro de transporte e só loga.
public interface INotificadorPainel
{
    Task ResumoAtualizadoAsync(Guid merchantId, CancellationToken ct);
}
