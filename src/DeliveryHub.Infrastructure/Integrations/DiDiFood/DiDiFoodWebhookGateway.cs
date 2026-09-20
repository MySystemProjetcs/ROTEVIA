using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeliveryHub.Infrastructure.Integrations.DiDiFood.Contracts;
using Microsoft.Extensions.Options;

namespace DeliveryHub.Infrastructure.Integrations.DiDiFood;

// O que a Api project pode saber sobre um webhook da 99Food, sem tocar no
// contrato interno completo (DiDiWebhookEvent/DiDiOrderModel ficam internal a
// Infrastructure). É a mesma fronteira ACL que o mapper já respeita para o
// domínio — aqui ela protege o limite entre Api e Infrastructure.
public sealed record DiDiWebhookRecebido(
    string EventId,
    string EventType,
    long OrderId,
    string? AppShopId);

public interface IDiDiFoodWebhookGateway
{
    // Nulo quando o JSON é ilegível ou a assinatura não confere — a Api não
    // precisa (nem deve) saber qual dos dois casos foi.
    DiDiWebhookRecebido? Validar(string payloadJson);
}

internal sealed class DiDiFoodWebhookGateway : IDiDiFoodWebhookGateway
{
    private readonly DiDiFoodOptions _opcoes;

    public DiDiFoodWebhookGateway(IOptions<DiDiFoodOptions> opcoes)
    {
        _opcoes = opcoes.Value;
    }

    public DiDiWebhookRecebido? Validar(string payloadJson)
    {
        DiDiWebhookEvent? evento;
        try
        {
            evento = JsonSerializer.Deserialize<DiDiWebhookEvent>(payloadJson);
        }
        catch (JsonException)
        {
            return null;
        }

        if (evento is null || !AssinaturaValida(evento))
            return null;

        return new DiDiWebhookRecebido(
            EventId: $"{evento.EventType}:{evento.OrderId}",
            EventType: evento.EventType ?? string.Empty,
            OrderId: evento.OrderId,
            AppShopId: evento.Order?.Shop?.AppShopId);
    }

    // MD5(app_id + timestamp + app_secret) — mesma fórmula do /shop/list.
    // Comparação em tempo constante: webhook é porta anônima, timing attack
    // permitiria descobrir qual parte da assinatura está correta.
    private bool AssinaturaValida(DiDiWebhookEvent evento)
    {
        if (string.IsNullOrEmpty(evento.Sign) || string.IsNullOrEmpty(_opcoes.AppSecret))
            return false;

        var entrada = $"{_opcoes.AppId}{evento.Timestamp}{_opcoes.AppSecret}";
        var esperado = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(entrada)));

        var a = Encoding.UTF8.GetBytes(evento.Sign.ToUpperInvariant());
        var b = Encoding.UTF8.GetBytes(esperado);

        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}
