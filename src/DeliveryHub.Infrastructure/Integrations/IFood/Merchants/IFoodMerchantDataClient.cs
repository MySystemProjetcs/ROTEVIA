using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DeliveryHub.Application.Abstractions;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Merchants;

internal sealed class IFoodMerchantDataClient
{
    // iFood exige corpo em camelCase ("storeId", "dayOfWeek", "start", "duration").
    // JsonContent.Create sem opções serializa PascalCase e a API devolve 400 por
    // "campos faltando". Esta opção é compartilhada entre as requisições com corpo
    // pra garantir que todas respeitem o contrato do iFood.
    private static readonly JsonSerializerOptions CamelCaseOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public IFoodMerchantDataClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<JsonElement> ObterDetalhesAsync(Guid merchantId, AuthenticationHeaderValue autorizacao, CancellationToken ct) =>
        GetAsync($"merchants/{merchantId}", autorizacao, ct);

    public Task<JsonElement> ObterStatusAsync(Guid merchantId, AuthenticationHeaderValue autorizacao, CancellationToken ct) =>
        GetAsync($"merchants/{merchantId}/status", autorizacao, ct);

    public Task<JsonElement> ObterStatusDaOperacaoAsync(
        Guid merchantId, string operacao, AuthenticationHeaderValue autorizacao, CancellationToken ct) =>
        GetAsync($"merchants/{merchantId}/status/{Uri.EscapeDataString(operacao)}", autorizacao, ct);

    public async Task<JsonElement> ObterHorarioDeFuncionamentoAsync(
        Guid merchantId, AuthenticationHeaderValue autorizacao, CancellationToken ct)
    {
        try
        {
            return await GetAsync($"merchants/{merchantId}/opening-hours", autorizacao, ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            using var documento = JsonDocument.Parse("[]");
            return documento.RootElement.Clone();
        }
    }

    public async Task<JsonElement> CriarHorarioDeFuncionamentoAsync(
        Guid merchantId,
        IFoodOpeningHoursInput input,
        AuthenticationHeaderValue autorizacao,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"merchants/{merchantId}/opening-hours")
        {
            Content = JsonContent.Create(
                new IFoodOpeningHoursRequest(merchantId, input.Shifts),
                options: CamelCaseOptions)
        };
        request.Headers.Authorization = autorizacao;

        using var response = await _httpClient.SendAsync(request, ct);
        await GarantirSucessoAsync(response, ct);

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return document.RootElement.Clone();
    }

    private async Task<JsonElement> GetAsync(string path, AuthenticationHeaderValue autorizacao, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = autorizacao;

        using var response = await _httpClient.SendAsync(request, ct);
        await GarantirSucessoAsync(response, ct);

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return document.RootElement.Clone();
    }

    // EnsureSuccessStatusCode descartaria o body {"error":{"code","message"}} que o
    // iFood devolve em 4xx/5xx — sem ele é impossível saber POR QUE um horário foi
    // recusado. Aqui o detalhe entra na mensagem da exceção (truncado) e o StatusCode
    // segue no terceiro argumento para os catches a jusante.
    private static async Task GarantirSucessoAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        var corpo = await response.Content.ReadAsStringAsync(ct);
        var detalhe = ExtrairMensagemDoIFood(corpo);
        var mensagem = $"O iFood respondeu {(int)response.StatusCode} ({response.ReasonPhrase}). {detalhe}";
        throw new HttpRequestException(mensagem, null, response.StatusCode);
    }

    private static string ExtrairMensagemDoIFood(string corpo)
    {
        const int limite = 500;
        try
        {
            using var documento = JsonDocument.Parse(corpo);
            var erro = documento.RootElement.TryGetProperty("error", out var no) ? no : documento.RootElement;
            var codigo = erro.TryGetProperty("code", out var c) ? c.GetString() : null;
            var mensagem = erro.TryGetProperty("message", out var m) ? m.GetString()
                : erro.TryGetProperty("name", out var n) ? n.GetString() : null;

            var texto = (codigo, mensagem) switch
            {
                (not null, not null) => $"{codigo}: {mensagem}",
                (null, not null) => mensagem,
                (not null, null) => codigo,
                _ => corpo
            };
            return texto.Length <= limite ? texto : texto[..limite] + "…";
        }
        catch (JsonException)
        {
            return corpo.Length <= limite ? corpo : corpo[..limite] + "…";
        }
    }

    private sealed record IFoodOpeningHoursRequest(Guid StoreId, IReadOnlyList<IFoodOpeningHourShift> Shifts);
}