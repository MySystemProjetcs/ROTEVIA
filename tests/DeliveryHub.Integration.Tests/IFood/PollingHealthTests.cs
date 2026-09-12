using DeliveryHub.Infrastructure.Integrations.IFood.Polling;

namespace DeliveryHub.Integration.Tests.IFood;

public sealed class PollingHealthTests
{
    [Fact]
    public void Comeca_sem_sucesso_registrado()
    {
        var health = new PollingHealth(new TestTimeProvider());

        Assert.Null(health.UltimoSucesso);
        Assert.Null(health.UltimaFalha);
    }

    [Fact]
    public void Registra_o_instante_do_sucesso()
    {
        var relogio = new TestTimeProvider();
        var health = new PollingHealth(relogio);

        var antes = relogio.GetUtcNow();
        health.RegistrarSucesso();

        Assert.Equal(antes, health.UltimoSucesso);
    }

    [Fact]
    public void Falha_nao_apaga_o_ultimo_sucesso()
    {
        // O que importa para o alarme é há quanto tempo não há sucesso. Se a
        // falha zerasse esse marcador, um ciclo ruim isolado apagaria a
        // evidência de que o polling estava saudável.
        var relogio = new TestTimeProvider();
        var health = new PollingHealth(relogio);

        health.RegistrarSucesso();
        var sucesso = health.UltimoSucesso;

        relogio.Advance(TimeSpan.FromSeconds(30));
        health.RegistrarFalha("timeout");

        Assert.Equal(sucesso, health.UltimoSucesso);
        Assert.Equal("timeout", health.UltimoErro);
        Assert.NotNull(health.UltimaFalha);
    }

    [Fact]
    public void Sucesso_posterior_avanca_o_marcador()
    {
        var relogio = new TestTimeProvider();
        var health = new PollingHealth(relogio);

        health.RegistrarSucesso();
        var primeiro = health.UltimoSucesso;

        relogio.Advance(TimeSpan.FromSeconds(30));
        health.RegistrarSucesso();

        Assert.True(health.UltimoSucesso > primeiro);
    }
}
