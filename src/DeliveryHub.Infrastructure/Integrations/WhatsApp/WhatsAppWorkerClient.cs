using System.Net;
using System.Net.Http.Json;
using DeliveryHub.Application.Abstractions;

namespace DeliveryHub.Infrastructure.Integrations.WhatsApp;

// Falha de status/erro vira exceção simples (sem Result<T>): não é o endpoint
// desta camada que decide o que fazer com uma falha do worker — quem chama
// decide, igual o WhatsAppSessionManager anterior já fazia.
internal sealed class WhatsAppWorkerClient : IWhatsAppWorkerClient
{
    private readonly HttpClient _http;

    public WhatsAppWorkerClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<StatusSessaoWorker> ConectarAsync(Guid merchantId, CancellationToken ct)
    {
        using var resposta = await _http.PostAsync($"/api/sessions/{merchantId}/connect", null, ct);
        resposta.EnsureSuccessStatusCode();
        return await LerStatusAsync(resposta, ct);
    }

    public async Task<StatusSessaoWorker> ObterStatusAsync(Guid merchantId, CancellationToken ct)
    {
        using var resposta = await _http.GetAsync($"/api/sessions/{merchantId}/status", ct);
        resposta.EnsureSuccessStatusCode();
        return await LerStatusAsync(resposta, ct);
    }

    public async Task DesconectarAsync(Guid merchantId, CancellationToken ct)
    {
        using var resposta = await _http.PostAsync($"/api/sessions/{merchantId}/disconnect", null, ct);
        resposta.EnsureSuccessStatusCode();
    }

    public async Task<MensagemEnviada> EnviarMensagemAsync(
        Guid merchantId, string telefone, string texto, string chaveIdempotencia, CancellationToken ct)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, $"/api/sessions/{merchantId}/messages")
        {
            Content = JsonContent.Create(new { to = telefone, text = texto })
        };
        requisicao.Headers.Add("Idempotency-Key", chaveIdempotencia);

        using var resposta = await _http.SendAsync(requisicao, ct);

        if (resposta.StatusCode == HttpStatusCode.Conflict)
            throw new InvalidOperationException("Sessão do WhatsApp não está conectada.");

        if (!resposta.IsSuccessStatusCode)
            throw new InvalidOperationException($"Worker WhatsApp recusou o envio: {await LerErroAsync(resposta, ct)}");

        var corpo = await resposta.Content.ReadFromJsonAsync<Dictionary<string, string?>>(ct);
        return new MensagemEnviada(corpo?.GetValueOrDefault("externalId"));
    }

    private static async Task<StatusSessaoWorker> LerStatusAsync(HttpResponseMessage resposta, CancellationToken ct)
    {
        var corpo = await resposta.Content.ReadFromJsonAsync<Dictionary<string, string?>>(ct);
        return new StatusSessaoWorker(
            corpo?.GetValueOrDefault("status") ?? "DISCONNECTED",
            corpo?.GetValueOrDefault("qrDataUrl"),
            corpo?.GetValueOrDefault("telefone"),
            corpo?.GetValueOrDefault("error"));
    }

    private static async Task<string> LerErroAsync(HttpResponseMessage resposta, CancellationToken ct)
    {
        try
        {
            var corpo = await resposta.Content.ReadFromJsonAsync<Dictionary<string, string?>>(ct);
            if (corpo?.GetValueOrDefault("error") is { Length: > 0 } erro)
                return erro;
        }
        catch
        {
            // corpo não é JSON válido: cai na mensagem genérica abaixo.
        }

        return $"HTTP {(int)resposta.StatusCode}";
    }
}
