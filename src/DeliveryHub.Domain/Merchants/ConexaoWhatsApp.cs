namespace DeliveryHub.Domain.Merchants;

public enum StatusConexaoWhatsApp
{
    Desconectado = 0,
    AguardandoLeituraDoQr = 1,
    Conectado = 2,
    Erro = 3
}

public enum DirecaoMensagemWhatsApp
{
    Enviada = 0,
    Recebida = 1
}

// ExternalId é o id do whatsapp-web.js (mensagem recebida) ou a chave de
// idempotência do envio (mensagem enviada) — é por ele que RegistrarMensagem
// decide se é reentrega/retry ou mensagem nova.
public sealed record MensagemWhatsApp(
    DirecaoMensagemWhatsApp Direcao, string Telefone, string Corpo, string ExternalId, DateTimeOffset OcorridoEm);

// QR em si não entra aqui: é efêmero (expira em segundos) e vive só na
// memória do worker Node — persistir no banco não teria valor.
public sealed record ConexaoWhatsApp(
    StatusConexaoWhatsApp Status, string? Telefone, DateTimeOffset? ConectadoEm,
    IReadOnlyList<MensagemWhatsApp> Historico);
