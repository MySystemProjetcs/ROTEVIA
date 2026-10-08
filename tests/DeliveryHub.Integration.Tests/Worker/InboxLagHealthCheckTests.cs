using DeliveryHub.Integration.Tests.Fakes;
using DeliveryHub.Worker.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DeliveryHub.Integration.Tests.Worker;

// Duas falhas distintas precisam de alarme: processador sem tick (morreu no
// meio) e fila que não drena (evento envelhecendo). Cada limite tem seu teste
// — se um deles regredir, o worker fica parado sem ninguém perceber.
public sealed class InboxLagHealthCheckTests
{
    private static readonly DateTimeOffset Agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static async Task<HealthCheckResult> Avaliar(InboxLagState estado, TestTimeProvider relogio) =>
        await new InboxLagHealthCheck(estado, relogio).CheckHealthAsync(new HealthCheckContext());

    private static InboxLagState EstadoComLeitura(int pendentes, DateTimeOffset? maisAntigo, DateTimeOffset em)
    {
        var estado = new InboxLagState(new TestTimeProvider());
        estado.Registrar(pendentes, maisAntigo, em);
        return estado;
    }

    [Fact]
    public async Task Antes_do_primeiro_tick_fica_degradado_e_nao_saudavel()
    {
        // Recém-subido o processador ainda não publicou nada — é espera, não
        // alarme, mas também não pode passar como saudável.
        var resultado = await Avaliar(new InboxLagState(new TestTimeProvider()), new TestTimeProvider());

        Assert.Equal(HealthStatus.Degraded, resultado.Status);
        Assert.Contains("não completou um tick", resultado.Description);
    }

    [Fact]
    public async Task Fila_vazia_com_tick_recente_e_saudavel()
    {
        var relogio = new TestTimeProvider();
        relogio.Definir(Agora);
        var estado = EstadoComLeitura(pendentes: 0, maisAntigo: null, em: Agora);

        relogio.Definir(Agora.AddSeconds(10));

        Assert.Equal(HealthStatus.Healthy, (await Avaliar(estado, relogio)).Status);
    }

    [Fact]
    public async Task Tick_parado_por_tres_minutos_vira_alarme()
    {
        // O processador publica a cada 2s; 180s sem publicar é worker travado,
        // não lote grande — e com ele parado todo evento novo fica preso.
        var relogio = new TestTimeProvider();
        relogio.Definir(Agora);
        var estado = EstadoComLeitura(pendentes: 3, maisAntigo: Agora, em: Agora);

        relogio.Definir(Agora.AddMinutes(3).AddSeconds(1));

        var resultado = await Avaliar(estado, relogio);

        Assert.Equal(HealthStatus.Unhealthy, resultado.Status);
        Assert.Contains("sem tick", resultado.Description);
    }

    [Fact]
    public async Task Evento_mais_antigo_com_quase_dois_minutos_degradado()
    {
        // Processador vivo (tick recente) mas backlog subindo: ainda drena,
        // só está lento — degrada, não grita.
        var relogio = new TestTimeProvider();
        relogio.Definir(Agora);
        var estado = EstadoComLeitura(
            pendentes: 40,
            maisAntigo: Agora.AddSeconds(-119),
            em: Agora);

        relogio.Definir(Agora.AddSeconds(2));

        var resultado = await Avaliar(estado, relogio);

        Assert.Equal(HealthStatus.Degraded, resultado.Status);
        Assert.Contains("backlog", resultado.Description.ToLowerInvariant());
    }

    [Fact]
    public async Task Evento_mais_antigo_com_cinco_minutos_vira_alarme()
    {
        // Cinco minutos com o mesmo evento na frente da fila é fila que não
        // drena (falha repetida ou processador travado entre ticks) — precisa
        // de gente, não de mais um ciclo.
        var relogio = new TestTimeProvider();
        relogio.Definir(Agora);
        var estado = EstadoComLeitura(
            pendentes: 10,
            maisAntigo: Agora.AddMinutes(-5).AddSeconds(-1),
            em: Agora);

        relogio.Definir(Agora.AddSeconds(1));

        var resultado = await Avaliar(estado, relogio);

        Assert.Equal(HealthStatus.Unhealthy, resultado.Status);
        Assert.Contains("fila não drena", resultado.Description);
    }

    [Fact]
    public async Task Saudavel_com_fila_jovem_e_tick_fresco()
    {
        var relogio = new TestTimeProvider();
        relogio.Definir(Agora);
        var estado = EstadoComLeitura(
            pendentes: 5,
            maisAntigo: Agora.AddSeconds(-30),
            em: Agora);

        relogio.Definir(Agora.AddSeconds(3));

        var resultado = await Avaliar(estado, relogio);

        Assert.Equal(HealthStatus.Healthy, resultado.Status);
        Assert.Equal(5, resultado.Data["pendentes"]);
    }
}
