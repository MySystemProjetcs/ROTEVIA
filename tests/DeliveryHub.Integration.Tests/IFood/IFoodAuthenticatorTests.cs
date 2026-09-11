using System.Net;
using DeliveryHub.Infrastructure.Integrations.IFood.Auth;
using Microsoft.Extensions.Options;

namespace DeliveryHub.Integration.Tests.IFood;

public sealed class IFoodAuthenticatorTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "ifood", "auth", name));

    private static (IFoodAuthenticator Authenticator, StubHttpMessageHandler Handler, TestTimeProvider Clock) Build(
        StubHttpMessageHandler handler)
    {
        var clock = new TestTimeProvider();
        var factory = new StubHttpClientFactory(handler, "https://merchant-api.ifood.com.br/authentication/v1.0/");

        var options = Options.Create(new IFoodOptions
        {
            ClientId = "client-id-de-teste",
            ClientSecret = "client-secret-de-teste"
        });

        return (new IFoodAuthenticator(factory, options, clock), handler, clock);
    }

    [Fact]
    public async Task Usa_o_esquema_devolvido_pelo_ifood_e_nao_um_valor_fixo()
    {
        var (authenticator, _, _) = Build(
            new StubHttpMessageHandler().Enqueue(HttpStatusCode.OK, Fixture("token-success.json")));

        var header = await authenticator.GetAuthorizationHeaderAsync(CancellationToken.None);

        Assert.Equal("bearer", header.Scheme);
        Assert.StartsWith("eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzUxMiJ9.", header.Parameter);
    }

    [Fact]
    public async Task Token_em_cache_nao_dispara_nova_requisicao()
    {
        var (authenticator, handler, _) = Build(
            new StubHttpMessageHandler().Enqueue(HttpStatusCode.OK, Fixture("token-success.json")));

        await authenticator.GetAuthorizationHeaderAsync(CancellationToken.None);
        await authenticator.GetAuthorizationHeaderAsync(CancellationToken.None);

        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Renova_a_noventa_por_cento_da_vida_util_do_token()
    {
        var (authenticator, handler, clock) = Build(new StubHttpMessageHandler()
            .Enqueue(HttpStatusCode.OK, Fixture("token-success.json"))
            .Enqueue(HttpStatusCode.OK, Fixture("token-success.json")));

        await authenticator.GetAuthorizationHeaderAsync(CancellationToken.None);

        // expiresIn do fixture = 21600s; 90% = 19440s (5h24)
        clock.Advance(TimeSpan.FromSeconds(19_439));
        await authenticator.GetAuthorizationHeaderAsync(CancellationToken.None);
        Assert.Equal(1, handler.RequestCount);

        clock.Advance(TimeSpan.FromSeconds(2));
        await authenticator.GetAuthorizationHeaderAsync(CancellationToken.None);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Margem_proporcional_sobrevive_a_token_de_vida_curta()
    {
        // Regressão: com margem fixa de 30 min, um expiresIn de 10 min jogava o
        // instante de renovação para o passado e fazia toda chamada reautenticar
        // — caminho direto para bloqueio por excesso de requisição.
        const string tokenCurto = """{"accessToken":"abc","type":"bearer","expiresIn":600}""";

        var (authenticator, handler, _) = Build(
            new StubHttpMessageHandler().Enqueue(HttpStatusCode.OK, tokenCurto));

        await authenticator.GetAuthorizationHeaderAsync(CancellationToken.None);
        await authenticator.GetAuthorizationHeaderAsync(CancellationToken.None);

        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task InvalidateCache_forca_nova_autenticacao()
    {
        var (authenticator, handler, _) = Build(new StubHttpMessageHandler()
            .Enqueue(HttpStatusCode.OK, Fixture("token-success.json"))
            .Enqueue(HttpStatusCode.OK, Fixture("token-success.json")));

        await authenticator.GetAuthorizationHeaderAsync(CancellationToken.None);
        authenticator.InvalidateCache();
        await authenticator.GetAuthorizationHeaderAsync(CancellationToken.None);

        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Erro_do_ifood_vira_excecao_com_codigo_e_mensagem()
    {
        var (authenticator, _, _) = Build(
            new StubHttpMessageHandler().Enqueue(HttpStatusCode.Forbidden, Fixture("error-forbidden.json")));

        var excecao = await Assert.ThrowsAsync<IFoodAuthenticationException>(
            () => authenticator.GetAuthorizationHeaderAsync(CancellationToken.None));

        Assert.Equal("Forbidden", excecao.IFoodErrorCode);
        Assert.Contains("No permissions granted", excecao.Message);
    }
}
