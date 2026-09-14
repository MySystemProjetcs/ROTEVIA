using System.Net;
using System.Net.Http.Headers;
using DeliveryHub.Infrastructure.Integrations.IFood.Auth;

namespace DeliveryHub.Integration.Tests.IFood;

public sealed class IFoodAuthorizationHandlerTests
{
    private sealed class FakeAuthenticator : IIFoodAuthenticator
    {
        private int _emitidos;

        public int InvalidateCount { get; private set; }

        public Task<AuthenticationHeaderValue> GetAuthorizationHeaderAsync(CancellationToken ct) =>
            Task.FromResult(new AuthenticationHeaderValue("bearer", $"token-{++_emitidos}"));

        public void InvalidateCache() => InvalidateCount++;
    }

    private static HttpClient Build(StubHttpMessageHandler stub, FakeAuthenticator authenticator) =>
        new(new IFoodAuthorizationHandler(authenticator) { InnerHandler = stub })
        {
            BaseAddress = new Uri("https://merchant-api.ifood.com.br/order/v1.0/")
        };

    [Fact]
    public async Task Anexa_o_authorization_em_toda_chamada()
    {
        var stub = new StubHttpMessageHandler().Enqueue(HttpStatusCode.OK, "{}");
        var httpClient = Build(stub, new FakeAuthenticator());

        await httpClient.GetAsync("orders/07110e1b-8191-4670-baed-407219481ffb");

        Assert.Equal("bearer", stub.AuthorizationHeaders[0]?.Scheme);
        Assert.Equal("token-1", stub.AuthorizationHeaders[0]?.Parameter);
    }

    [Fact]
    public async Task No_401_troca_o_token_e_repete_a_requisicao_uma_vez()
    {
        var stub = new StubHttpMessageHandler()
            .Enqueue(HttpStatusCode.Unauthorized, "{}")
            .Enqueue(HttpStatusCode.OK, "{}");

        var authenticator = new FakeAuthenticator();
        var httpClient = Build(stub, authenticator);

        var resposta = await httpClient.GetAsync("orders/07110e1b-8191-4670-baed-407219481ffb");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(2, stub.RequestCount);
        Assert.Equal(1, authenticator.InvalidateCount);
        Assert.Equal("token-1", stub.AuthorizationHeaders[0]?.Parameter);
        Assert.Equal("token-2", stub.AuthorizationHeaders[1]?.Parameter);
    }

    [Fact]
    public async Task O_retry_preserva_o_corpo_da_requisicao()
    {
        // O acknowledgment é POST: se o clone perder o corpo, o retry manda uma
        // lista vazia e os eventos ficam sem reconhecimento.
        var stub = new StubHttpMessageHandler()
            .Enqueue(HttpStatusCode.Unauthorized, "{}")
            .Enqueue(HttpStatusCode.Accepted, "{}");

        var httpClient = Build(stub, new FakeAuthenticator());

        await httpClient.PostAsync(
            "events/acknowledgment",
            new StringContent("""[{"id":"cd40582b-0ef2-4d52-bc7c-507fdff12e21"}]""", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(2, stub.RequestCount);
        Assert.Contains("cd40582b-0ef2-4d52-bc7c-507fdff12e21", stub.LastRequestBody);
    }

    [Fact]
    public async Task Nao_repete_requisicao_no_403()
    {
        // A doc do iFood é explícita: "Evite retentativas automáticas em 403;
        // retente apenas após ajustar permissões". 403 é falta de permissão do
        // módulo ou do merchant — repetir não resolve e só queima rate limit.
        var stub = new StubHttpMessageHandler().Enqueue(HttpStatusCode.Forbidden, "{}");
        var authenticator = new FakeAuthenticator();
        var httpClient = Build(stub, authenticator);

        var resposta = await httpClient.GetAsync("orders/07110e1b-8191-4670-baed-407219481ffb");

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.Equal(1, stub.RequestCount);
        Assert.Equal(0, authenticator.InvalidateCount);
    }

    [Fact]
    public async Task Autorizacao_explicita_nao_e_sobrescrita_pelo_autenticador_Centralizado()
    {
        // Uma loja do fluxo Distribuído já chega com o próprio token anexado.
        // Se o handler sobrescrevesse, a chamada usaria o token errado — de
        // uma loja diferente da que ela deveria enxergar.
        var stub = new StubHttpMessageHandler().Enqueue(HttpStatusCode.OK, "{}");
        var httpClient = Build(stub, new FakeAuthenticator());

        using var request = new HttpRequestMessage(HttpMethod.Get, "orders/07110e1b-8191-4670-baed-407219481ffb")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("bearer", "token-da-loja-distribuida") }
        };
        await httpClient.SendAsync(request);

        Assert.Equal("token-da-loja-distribuida", stub.AuthorizationHeaders[0]?.Parameter);
    }

    [Fact]
    public async Task Autorizacao_explicita_com_401_nao_invalida_o_cache_Centralizado_nem_repete()
    {
        // Um 401 na loja do Distribuído é problema do token daquela loja — não
        // tem relação com o autenticador Centralizado, que continua válido.
        var stub = new StubHttpMessageHandler().Enqueue(HttpStatusCode.Unauthorized, "{}");
        var authenticator = new FakeAuthenticator();
        var httpClient = Build(stub, authenticator);

        using var request = new HttpRequestMessage(HttpMethod.Get, "orders/07110e1b-8191-4670-baed-407219481ffb")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("bearer", "token-da-loja-distribuida") }
        };
        var resposta = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal(1, stub.RequestCount);
        Assert.Equal(0, authenticator.InvalidateCount);
    }

    [Fact]
    public async Task Resposta_bem_sucedida_nao_dispara_retry()
    {
        var stub = new StubHttpMessageHandler().Enqueue(HttpStatusCode.OK, "{}");
        var authenticator = new FakeAuthenticator();
        var httpClient = Build(stub, authenticator);

        await httpClient.GetAsync("orders/07110e1b-8191-4670-baed-407219481ffb");

        Assert.Equal(1, stub.RequestCount);
        Assert.Equal(0, authenticator.InvalidateCount);
    }
}
