using DeliveryHub.Infrastructure.Integrations.IFood.Polling;
using DeliveryHub.Worker;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DeliveryHub.Integration.Tests.IFood;

// Relógio controlado em vez de esperar 95 segundos reais. O caminho que
// realmente importa é o Unhealthy: se ele não disparar, o worker pode ficar
// travado sem ninguém saber, com as lojas offline no iFood.
public sealed class PollingHealthCheckTests
{
    private static async Task<HealthCheckResult> Avaliar(PollingHealth health, TestTimeProvider relogio) =>
        await new PollingHealthCheck(health, relogio).CheckHealthAsync(new HealthCheckContext());

    [Fact]
    public async Task Antes_do_primeiro_ciclo_fica_degradado_e_nao_saudavel()
    {
        // Recém-subido ainda não é saudável — mas também não é alarme.
        var relogio = new TestTimeProvider();

        var resultado = await Avaliar(new PollingHealth(relogio), relogio);

        Assert.Equal(HealthStatus.Degraded, resultado.Status);
    }

    [Fact]
    public async Task Polling_recente_e_saudavel()
    {
        var relogio = new TestTimeProvider();
        var health = new PollingHealth(relogio);
        health.RegistrarSucesso();

        relogio.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(HealthStatus.Healthy, (await Avaliar(health, relogio)).Status);
    }

    [Fact]
    public async Task Um_ciclo_perdido_ainda_e_saudavel()
    {
        // Falha isolada é ruído de rede: alarme a cada soluço vira alarme
        // ignorado.
        var relogio = new TestTimeProvider();
        var health = new PollingHealth(relogio);
        health.RegistrarSucesso();

        relogio.Advance(TimeSpan.FromSeconds(65));

        Assert.Equal(HealthStatus.Healthy, (await Avaliar(health, relogio)).Status);
    }

    [Fact]
    public async Task Tres_ciclos_perdidos_viram_alarme()
    {
        var relogio = new TestTimeProvider();
        var health = new PollingHealth(relogio);
        health.RegistrarSucesso();

        relogio.Advance(TimeSpan.FromSeconds(100));

        var resultado = await Avaliar(health, relogio);

        Assert.Equal(HealthStatus.Unhealthy, resultado.Status);
        Assert.Contains("offline no iFood", resultado.Description);
    }

    [Fact]
    public async Task Worker_vivo_mas_falhando_dispara_alarme()
    {
        // O caso mais perigoso: processo de pé, respondendo ao health check,
        // mas sem completar ciclo nenhum. Se olhássemos só se o processo está
        // vivo, isto passaria despercebido.
        var relogio = new TestTimeProvider();
        var health = new PollingHealth(relogio);
        health.RegistrarSucesso();

        for (var i = 0; i < 4; i++)
        {
            relogio.Advance(TimeSpan.FromSeconds(30));
            health.RegistrarFalha("iFood indisponível");
        }

        var resultado = await Avaliar(health, relogio);

        Assert.Equal(HealthStatus.Unhealthy, resultado.Status);
        Assert.Equal("iFood indisponível", resultado.Data["ultimoErro"]);
    }
}
