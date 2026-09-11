using System.Net;
using DeliveryHub.Infrastructure.Integrations.IFood;
using DeliveryHub.Infrastructure.Integrations.IFood.Polling;

namespace DeliveryHub.Integration.Tests.IFood;

public sealed class IFoodEventsClientTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "ifood", "events", name));

    private static IFoodEventsClient Build(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://merchant-api.ifood.com.br/events/v1.0/")
        });

    [Fact]
    public async Task Polling_le_os_eventos_da_resposta()
    {
        var handler = new StubHttpMessageHandler().Enqueue(HttpStatusCode.OK, Fixture("polling-events.json"));
        var client = Build(handler);

        var eventos = await client.PollAsync(CancellationToken.None);

        // "events:polling" como Uri relativo seria lido como esquema "events";
        // a URL final precisa mesmo bater com o endpoint documentado.
        Assert.Equal(
            "https://merchant-api.ifood.com.br/events/v1.0/events:polling",
            handler.LastRequestUri?.ToString());

        Assert.Equal(3, eventos.Count);
        Assert.Equal("CONFIRMED", eventos[0].FullCode);
        Assert.Equal(Guid.Parse("93ba4bf4-f4ae-4de8-8017-35d7c7de9bf1"), eventos[0].OrderId);

        // É o merchantId do evento que resolve o tenant já na ingestão.
        Assert.Equal(Guid.Parse("820af392-002c-47b1-bfae-d7ef31743c99"), eventos[0].MerchantId);
        Assert.Equal("IFOOD", eventos[0].SalesChannel);

        // Evento sem metadata existe (RETURN_TO_STORE do exemplo da doc).
        Assert.Null(eventos[2].Metadata);
    }

    [Theory]
    [InlineData("2021-02-17T19:36:55.295Z", 295)]
    [InlineData("2021-02-17T19:36:55.2Z", 200)]
    [InlineData("2021-02-17T19:36:55Z", 0)]
    public async Task CreatedAt_aceita_as_tres_precisoes_de_fracao_de_segundo(string createdAt, int milissegundos)
    {
        // O iFood omite zeros à direita na fração de segundo: ".2" significa
        // 200ms, não 2ms. Um parser que assuma 3 dígitos fixos quebra no polling.
        var json = $$"""
            [{"id":"b03392c5-61dd-47c4-a503-bce3109c96c8","orderId":"93ba4bf4-f4ae-4de8-8017-35d7c7de9bf1",
              "merchantId":"820af392-002c-47b1-bfae-d7ef31743c99","code":"CFM","fullCode":"CONFIRMED",
              "salesChannel":"IFOOD","createdAt":"{{createdAt}}"}]
            """;

        var client = Build(new StubHttpMessageHandler().Enqueue(HttpStatusCode.OK, json));

        var eventos = await client.PollAsync(CancellationToken.None);

        Assert.Equal(milissegundos, eventos[0].CreatedAt.Millisecond);
        Assert.Equal(TimeSpan.Zero, eventos[0].CreatedAt.Offset);
    }

    [Fact]
    public async Task Polling_sem_evento_novo_devolve_lista_vazia()
    {
        // O iFood responde 204 com corpo vazio quando não há evento; tentar
        // desserializar isso estoura o polling e derruba o heartbeat.
        var client = Build(new StubHttpMessageHandler().Enqueue(HttpStatusCode.NoContent, string.Empty));

        var eventos = await client.PollAsync(CancellationToken.None);

        Assert.Empty(eventos);
    }

    [Fact]
    public async Task Polling_403_diz_quais_lojas_perderam_autorizacao()
    {
        var client = Build(new StubHttpMessageHandler()
            .Enqueue(HttpStatusCode.Forbidden, Fixture("polling-403-unauthorized.json")));

        var excecao = await Assert.ThrowsAsync<IFoodApiException>(() => client.PollAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.Forbidden, excecao.StatusCode);
        Assert.Equal(2, excecao.UnauthorizedMerchants.Count);
        Assert.Contains(Guid.Parse("6b487a27-c4fc-4f26-b05e-3967c2331882"), excecao.UnauthorizedMerchants);
    }

    [Fact]
    public async Task Acknowledgment_sem_eventos_nao_chama_a_api()
    {
        var handler = new StubHttpMessageHandler();
        var client = Build(handler);

        await client.AcknowledgeAsync([], CancellationToken.None);

        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task Acknowledgment_envia_os_ids_recebidos()
    {
        var handler = new StubHttpMessageHandler().Enqueue(HttpStatusCode.Accepted, string.Empty);
        var client = Build(handler);

        await client.AcknowledgeAsync([Guid.Parse("cd40582b-0ef2-4d52-bc7c-507fdff12e21")], CancellationToken.None);

        Assert.Equal(1, handler.RequestCount);
        Assert.Contains("cd40582b-0ef2-4d52-bc7c-507fdff12e21", handler.LastRequestBody);
    }
}
