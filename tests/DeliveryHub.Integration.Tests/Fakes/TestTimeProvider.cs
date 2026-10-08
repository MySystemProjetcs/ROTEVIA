namespace DeliveryHub.Integration.Tests.Fakes;

internal sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan elapsed) => _now += elapsed;

    // Definir o relógio num ponto exato: testes de health check precisam de
    // "agora" conhecido em vez de acumular advances.
    public void Definir(DateTimeOffset momento) => _now = momento;
}
