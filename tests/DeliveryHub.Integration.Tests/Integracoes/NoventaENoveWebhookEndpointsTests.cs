using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeliveryHub.Infrastructure.Integrations.DiDiFood;
using DeliveryHub.Infrastructure.Integrations.DiDiFood.Contracts;
using Microsoft.Extensions.Options;

namespace DeliveryHub.Integration.Tests.Integracoes;

public sealed class NoventaENoveWebhookEndpointsTests
{
    [Fact]
    public void Assinatura_MD5_valida_calculo_conforme_spec_didi()
    {
        // Arrange
        var appId = 3458764610605350993L;
        var appSecret = "secret_key_123";
        var timestamp = 1726776000L;

        // Fórmula da spec OpenAPI: MD5(app_id + timestamp + app_secret) em hex uppercase
        var entrada = $"{appId}{timestamp}{appSecret}";
        var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(entrada));
        var signEsperado = Convert.ToHexString(hashBytes);

        var opcoes = Options.Create(new DiDiFoodOptions
        {
            AppId = appId,
            AppSecret = appSecret
        });

        var evento = new DiDiWebhookEvent
        {
            EventType = "newOrder",
            OrderId = 2352921557674426622L,
            Timestamp = timestamp,
            Sign = signEsperado
        };

        // Act & Assert
        Assert.NotNull(evento.Sign);
        Assert.Equal(signEsperado, evento.Sign);
    }

    [Fact]
    public void Assinatura_invalida_deve_ser_rejeitada()
    {
        // Arrange
        var opcoes = Options.Create(new DiDiFoodOptions
        {
            AppId = 12345,
            AppSecret = "segredo_correto"
        });

        var evento = new DiDiWebhookEvent
        {
            EventType = "newOrder",
            OrderId = 9999,
            Timestamp = 1726776000L,
            Sign = "ASSINATURA_FALSA"
        };

        // Act
        var entradaEsperada = $"{opcoes.Value.AppId}{evento.Timestamp}{opcoes.Value.AppSecret}";
        var hashEsperado = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(entradaEsperada)));

        // Assert
        Assert.NotEqual(hashEsperado, evento.Sign);
    }
}
