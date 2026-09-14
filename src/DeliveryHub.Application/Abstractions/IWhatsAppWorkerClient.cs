namespace DeliveryHub.Application.Abstractions;

// Espelha a resposta crua do worker Node (whatsapp-worker/) — Status vem no
// vocabulário dele (QR_PENDING, SCANNING, CONNECTED, DISCONNECTED, ERROR).
// Quem traduz pra StatusConexaoWhatsApp é o endpoint, não este cliente: o
// cliente só fala HTTP, não conhece o domínio.
public sealed record StatusSessaoWorker(string Status, string? QrDataUrl, string? Telefone, string? Erro);

public sealed record MensagemEnviada(string? ExternalId);

public interface IWhatsAppWorkerClient
{
    Task<StatusSessaoWorker> ConectarAsync(Guid merchantId, CancellationToken ct);
    Task<StatusSessaoWorker> ObterStatusAsync(Guid merchantId, CancellationToken ct);
    Task DesconectarAsync(Guid merchantId, CancellationToken ct);

    // chaveIdempotencia vai em Idempotency-Key: um retry de rede da API não
    // reenvia a mesma mensagem duas vezes pro WhatsApp.
    Task<MensagemEnviada> EnviarMensagemAsync(
        Guid merchantId, string telefone, string texto, string chaveIdempotencia, CancellationToken ct);
}
