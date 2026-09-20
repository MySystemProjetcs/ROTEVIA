using System.Security.Cryptography;
using System.Text;
using DeliveryHub.Infrastructure.Integrations.DiDiFood;
using Microsoft.Extensions.Options;

namespace DeliveryHub.Integration.Tests.Integracoes;

// Testa o gateway de verdade (DiDiFoodWebhookGateway.Validar), não uma cópia
// da fórmula MD5 ao lado dela — os dois testes anteriores recalculavam a
// mesma fórmula e comparavam consigo mesmos, sem nunca chamar o código de
// produção (CLAUDE.md §10: não escrever teste que só repete a implementação).
public sealed class NoventaENoveWebhookEndpointsTests
{
    private const long AppId = 3458764610605350993L;
    private const string AppSecret = "secret_key_123";

    private static DiDiFoodWebhookGateway CriarGateway() =>
        new(Options.Create(new DiDiFoodOptions { AppId = AppId, AppSecret = AppSecret }));

    private static string AssinaturaValida(long timestamp) =>
        Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes($"{AppId}{timestamp}{AppSecret}")));

    private static string PayloadCom(string eventType, long orderId, long timestamp, string sign, string? appShopId = "loja-001") =>
        $$"""
        {
          "event_type": "{{eventType}}",
          "order_id": {{orderId}},
          "timestamp": {{timestamp}},
          "sign": "{{sign}}",
          "order": { "order_id": {{orderId}}, "shop": { "app_shop_id": {{(appShopId is null ? "null" : $"\"{appShopId}\"")}} } }
        }
        """;

    [Fact]
    public void Validar_com_assinatura_correta_extrai_os_campos_do_evento()
    {
        var timestamp = 1726776000L;
        var payload = PayloadCom("orderCancel", 2352921557674426622L, timestamp, AssinaturaValida(timestamp));

        var recebido = CriarGateway().Validar(payload);

        Assert.NotNull(recebido);
        Assert.Equal("orderCancel", recebido.EventType);
        Assert.Equal(2352921557674426622L, recebido.OrderId);
        Assert.Equal("loja-001", recebido.AppShopId);
        Assert.Equal("orderCancel:2352921557674426622", recebido.EventId);
    }

    [Fact]
    public void Validar_com_assinatura_falsa_e_recusado()
    {
        var payload = PayloadCom("orderNew", 9999, 1726776000L, "ASSINATURA_FALSA");

        var recebido = CriarGateway().Validar(payload);

        Assert.Null(recebido);
    }

    [Fact]
    public void Validar_com_timestamp_diferente_do_assinado_e_recusado()
    {
        // Assina para um timestamp e manda outro no corpo — simula replay ou
        // adulteração; o hash não bate porque o timestamp entra na fórmula.
        var payload = PayloadCom("orderFinish", 1234, 1726776999L, AssinaturaValida(1726776000L));

        var recebido = CriarGateway().Validar(payload);

        Assert.Null(recebido);
    }

    [Fact]
    public void Validar_com_json_ilegivel_nao_lanca_e_devolve_nulo()
    {
        var recebido = CriarGateway().Validar("{ isto não é json");

        Assert.Null(recebido);
    }

    [Fact]
    public void Validar_sem_app_shop_id_devolve_appshopid_nulo_sem_falhar()
    {
        var timestamp = 1726776000L;
        var payload = PayloadCom("deliveryStatus", 555, timestamp, AssinaturaValida(timestamp), appShopId: null);

        var recebido = CriarGateway().Validar(payload);

        Assert.NotNull(recebido);
        Assert.Null(recebido.AppShopId);
    }
}
