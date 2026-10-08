using System.Net.Http.Headers;
using System.Net.Http.Json;
using DeliveryHub.Infrastructure.Integrations.IFood.Contracts;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Orders;

internal interface IIFoodOrderClient
{
    // O evento do polling não diz de qual loja é o pedido — o merchant só
    // aparece aqui, e é por ele que o tenant é resolvido. autorizacao nulo usa
    // o token Centralizado padrão; uma loja do Distribuído passa o próprio.
    Task<IFoodOrderDetails> GetDetailsAsync(Guid orderId, CancellationToken ct, AuthenticationHeaderValue? autorizacao = null);

    Task ConfirmAsync(Guid orderId, CancellationToken ct);
    Task StartPreparationAsync(Guid orderId, CancellationToken ct);
    Task ReadyToPickupAsync(Guid orderId, CancellationToken ct);
    Task DispatchAsync(Guid orderId, CancellationToken ct);

    // Solicita o cancelamento no iFood. Manda corpo (motivo + código de
    // cancelamento) — diferente das ações acima, que são POST sem corpo.
    Task RequestCancellationAsync(Guid orderId, string reason, string cancellationCode, CancellationToken ct);

    // Lista os códigos de cancelamento válidos para o status corrente do
    // pedido. Pode vir vazio quando o pedido não aceita mais cancelamento.
    Task<IReadOnlyList<IFoodCancellationReason>> GetCancellationReasonsAsync(Guid orderId, CancellationToken ct);

    // Diferente das demais: manda corpo e lê a resposta. Devolve se o código
    // confere — código errado é 200 com valid:false, não erro HTTP.
    Task<bool> VerifyDeliveryCodeAsync(Guid orderId, string code, CancellationToken ct);
    Task<bool> ValidatePickupCodeAsync(Guid orderId, string code, CancellationToken ct);
}

internal sealed class IFoodOrderClient : IIFoodOrderClient
{
    private readonly HttpClient _httpClient;

    public IFoodOrderClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IFoodOrderDetails> GetDetailsAsync(
        Guid orderId, CancellationToken ct, AuthenticationHeaderValue? autorizacao = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"orders/{orderId}");
        if (autorizacao is not null)
            request.Headers.Authorization = autorizacao;

        using var response = await _httpClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<IFoodErrorResponse>(ct);
            throw new IFoodApiException(
                response.StatusCode,
                error?.Error.Message ?? $"Falha ao buscar o pedido no iFood (HTTP {(int)response.StatusCode}).",
                error?.Error.Code);
        }

        return await response.Content.ReadFromJsonAsync<IFoodOrderDetails>(ct)
            ?? throw new IFoodApiException(response.StatusCode, "Detalhe do pedido veio vazio.");
    }

    // Todas as ações são POST sem corpo e respondem 202 Accepted; a mudança de
    // status chega depois como evento do polling.
    public Task ConfirmAsync(Guid orderId, CancellationToken ct) =>
        AcionarAsync(orderId, "confirm", ct);

    public Task StartPreparationAsync(Guid orderId, CancellationToken ct) =>
        AcionarAsync(orderId, "startPreparation", ct);

    public Task ReadyToPickupAsync(Guid orderId, CancellationToken ct) =>
        AcionarAsync(orderId, "readyToPickup", ct);

    public Task DispatchAsync(Guid orderId, CancellationToken ct) =>
        AcionarAsync(orderId, "dispatch", ct);

    // Busca os códigos de cancelamento aceitos agora para este pedido. O
    // adapter chama antes do requestCancellation para escolher um código
    // válido para o status corrente — sem isso, um valor fixo pode ser
    // recusado e o lojista vê "A origem recusou a operação".
    public async Task<IReadOnlyList<IFoodCancellationReason>> GetCancellationReasonsAsync(
        Guid orderId, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync($"orders/{orderId}/cancellationReasons", ct);

        if (!response.IsSuccessStatusCode)
        {
            var corpoCru = await response.Content.ReadAsStringAsync(ct);
            var codigoErro = default(string);
            var mensagem = $"O iFood recusou a listagem de motivos (HTTP {(int)response.StatusCode}). Corpo: {corpoCru}";
            try
            {
                var error = System.Text.Json.JsonSerializer.Deserialize<IFoodErrorResponse>(corpoCru);
                if (error is not null)
                {
                    codigoErro = error.Error.Code;
                    mensagem = error.Error.Message;
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Corpo fora do formato esperado — segue com o corpo cru.
            }

            throw new IFoodApiException(response.StatusCode, mensagem, codigoErro);
        }

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<IFoodCancellationReason>>(ct)
            ?? Array.Empty<IFoodCancellationReason>();
    }

    // O corpo carrega o motivo em texto e o cancellationCode do iFood (o código
    // válido varia por pedido — por isso o adapter busca a lista real via
    // GetCancellationReasonsAsync antes de chamar aqui).
    public async Task RequestCancellationAsync(
        Guid orderId, string reason, string cancellationCode, CancellationToken ct)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"orders/{orderId}/requestCancellation",
            new IFoodRequestCancellationRequest(reason, cancellationCode),
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var corpoCru = await response.Content.ReadAsStringAsync(ct);
            var codigoErro = default(string);
            var mensagem = $"O iFood recusou o cancelamento (HTTP {(int)response.StatusCode}). Corpo: {corpoCru}";
            try
            {
                var error = System.Text.Json.JsonSerializer.Deserialize<IFoodErrorResponse>(corpoCru);
                if (error is not null)
                {
                    codigoErro = error.Error.Code;
                    mensagem = error.Error.Message;
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Corpo fora do formato esperado — segue com o corpo cru na
                // mensagem, que é o que precisamos ver pra diagnosticar.
            }

            throw new IFoodApiException(response.StatusCode, mensagem, codigoErro);
        }
    }

    // Não dá para reusar o AcionarAsync: ele posta sem corpo e descarta a
    // resposta, e aqui os dois importam.
    public async Task<bool> VerifyDeliveryCodeAsync(Guid orderId, string code, CancellationToken ct)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"orders/{orderId}/verifyDeliveryCode",
            new IFoodVerifyDeliveryCodeRequest(code),
            ct);

        if (!response.IsSuccessStatusCode)
        {
            // 412 tem significado próprio na doc do iFood: "o status do
            // handshake não é válido; o pedido deve ter sido aceito pelo
            // motorista antes da validação". Mensagem específica para o
            // motoboy saber o que fazer (aceitar a entrega antes), em vez de
            // ver um "código recusado" genérico.
            if ((int)response.StatusCode == 412)
            {
                throw new IFoodApiException(
                    response.StatusCode,
                    "Aceite a entrega antes de validar o código.",
                    "ifood.handshake_invalido");
            }

            // Corpo cru: quando o iFood devolve 400 com formato diferente do
            // IFoodErrorResponse (ou vazio), ler direto como string é a única
            // forma de descobrir o que ele está reclamando. O `code` do
            // motoboy nunca aparece aqui — só o que veio de resposta.
            var corpoCru = await response.Content.ReadAsStringAsync(ct);

            var codigoErro = default(string);
            var mensagem = $"O iFood recusou a validação do código (HTTP {(int)response.StatusCode}). Corpo: {corpoCru}";
            try
            {
                var error = System.Text.Json.JsonSerializer.Deserialize<IFoodErrorResponse>(corpoCru);
                if (error is not null)
                {
                    codigoErro = error.Error.Code;
                    mensagem = error.Error.Message;
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Corpo não é JSON ou não é o formato esperado — segue com o
                // corpo cru na mensagem, que é justamente o que precisamos ver
                // pra diagnosticar. Não relança: é caminho previsto.
            }

            throw new IFoodApiException(response.StatusCode, mensagem, codigoErro);
        }

        var corpo = await response.Content.ReadFromJsonAsync<IFoodVerifyDeliveryCodeResponse>(ct);

        return corpo?.Success ?? false;
    }

    // Fluxo gêmeo do verifyDeliveryCode, mas no endpoint de coleta. O 404 do
    // iFood aqui tem significado específico documentado: "pedido não encontrado
    // (verifique se é um pedido de autoentrega)" — ou seja, só pedidos entregues
    // pela própria loja (deliveredBy=MERCHANT) têm pickupCode. Em pedido
    // entregue pelo iFood a chamada 404 a indica "endpoint não se aplica aqui".
    public async Task<bool> ValidatePickupCodeAsync(Guid orderId, string code, CancellationToken ct)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"orders/{orderId}/validatePickupCode",
            new IFoodValidatePickupCodeRequest(code),
            ct);

        if (!response.IsSuccessStatusCode)
        {
            if ((int)response.StatusCode == 412)
            {
                throw new IFoodApiException(
                    response.StatusCode,
                    "Aceite o pedido antes de validar o código de coleta.",
                    "ifood.handshake_invalido");
            }

            var corpoCru = await response.Content.ReadAsStringAsync(ct);

            var codigoErro = default(string);
            var mensagem = $"O iFood recusou a validação da coleta (HTTP {(int)response.StatusCode}). Corpo: {corpoCru}";
            try
            {
                var error = System.Text.Json.JsonSerializer.Deserialize<IFoodErrorResponse>(corpoCru);
                if (error is not null)
                {
                    codigoErro = error.Error.Code;
                    mensagem = error.Error.Message;
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Corpo inesperado: segue com o cru para log/diagnóstico.
            }

            throw new IFoodApiException(response.StatusCode, mensagem, codigoErro);
        }

        var corpo = await response.Content.ReadFromJsonAsync<IFoodValidatePickupCodeResponse>(ct);
        return corpo?.Success ?? false;
    }

    private async Task AcionarAsync(Guid orderId, string acao, CancellationToken ct)
    {
        using var response = await _httpClient.PostAsync($"orders/{orderId}/{acao}", content: null, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<IFoodErrorResponse>(ct);
            throw new IFoodApiException(
                response.StatusCode,
                error?.Error.Message ?? $"O iFood recusou a ação '{acao}' (HTTP {(int)response.StatusCode}).",
                error?.Error.Code);
        }
    }
}
