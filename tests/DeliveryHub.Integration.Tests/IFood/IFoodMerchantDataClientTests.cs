using System.Net;
using System.Net.Http.Headers;
using DeliveryHub.Application.Abstractions;
using DeliveryHub.Infrastructure.Integrations.IFood.Merchants;

namespace DeliveryHub.Integration.Tests.IFood;

public sealed class IFoodMerchantDataClientTests
{
    private static readonly Guid MerchantId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

    private static IFoodMerchantDataClient Build(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://merchant-api.ifood.com.br/merchant/v1.0/")
        });

    [Fact]
    public async Task Obtem_detalhes_com_token_distribuido()
    {
        var handler = new StubHttpMessageHandler().Enqueue(HttpStatusCode.OK, """{"id":"3fa85f64-5717-4562-b3fc-2c963f66afa6","name":"Loja"}""");
        var client = Build(handler);

        var detalhes = await client.ObterDetalhesAsync(
            MerchantId, new AuthenticationHeaderValue("bearer", "token-loja"), CancellationToken.None);

        Assert.Equal("https://merchant-api.ifood.com.br/merchant/v1.0/merchants/3fa85f64-5717-4562-b3fc-2c963f66afa6", handler.LastRequestUri?.ToString());
        Assert.Equal("bearer", handler.AuthorizationHeaders.Single()?.Scheme);
        Assert.Equal("token-loja", handler.AuthorizationHeaders.Single()?.Parameter);
        Assert.Equal("Loja", detalhes.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Obtem_status_agregado_por_operacao_e_horarios()
    {
        var handler = new StubHttpMessageHandler()
            .Enqueue(HttpStatusCode.OK, "[]")
            .Enqueue(HttpStatusCode.OK, "{}")
            .Enqueue(HttpStatusCode.OK, "[]");
        var client = Build(handler);
        var autorizacao = new AuthenticationHeaderValue("bearer", "token-loja");

        await client.ObterStatusAsync(MerchantId, autorizacao, CancellationToken.None);
        Assert.EndsWith("/merchants/3fa85f64-5717-4562-b3fc-2c963f66afa6/status", handler.LastRequestUri?.ToString());

        await client.ObterStatusDaOperacaoAsync(MerchantId, "DELIVERY", autorizacao, CancellationToken.None);
        Assert.EndsWith("/merchants/3fa85f64-5717-4562-b3fc-2c963f66afa6/status/DELIVERY", handler.LastRequestUri?.ToString());

        await client.ObterHorarioDeFuncionamentoAsync(MerchantId, autorizacao, CancellationToken.None);
        Assert.EndsWith("/merchants/3fa85f64-5717-4562-b3fc-2c963f66afa6/openingHours", handler.LastRequestUri?.ToString());
        Assert.All(handler.AuthorizationHeaders, header => Assert.Equal("token-loja", header?.Parameter));
    }

    [Fact]
    public async Task Propaga_status_http_de_erro_do_ifood()
    {
        var client = Build(new StubHttpMessageHandler().Enqueue(HttpStatusCode.Forbidden, "{}"));

        var erro = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.ObterStatusAsync(MerchantId, new AuthenticationHeaderValue("bearer", "token-loja"), CancellationToken.None));

        Assert.Equal(HttpStatusCode.Forbidden, erro.StatusCode);
    }

    [Fact]
    public async Task Inclui_mensagem_do_ifood_no_erro()
    {
        var handler = new StubHttpMessageHandler().Enqueue(
            HttpStatusCode.BadRequest,
            """{"error":{"code":"INVALID_SHIFTS","message":"Overlapping shifts on MONDAY"}}""");
        var client = Build(handler);

        var erro = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.CriarHorarioDeFuncionamentoAsync(
                MerchantId,
                new IFoodOpeningHoursInput([new IFoodOpeningHourShift("MONDAY", "10:00:00", 480)]),
                new AuthenticationHeaderValue("bearer", "token-loja"),
                CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadRequest, erro.StatusCode);
        Assert.Contains("Overlapping shifts on MONDAY", erro.Message);
        Assert.Contains("INVALID_SHIFTS", erro.Message);
    }
}