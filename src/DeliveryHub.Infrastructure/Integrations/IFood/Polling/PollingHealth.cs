namespace DeliveryHub.Infrastructure.Integrations.IFood.Polling;

// O polling é o heartbeat da loja no iFood: sem requisição regular a cada 30s
// a loja é marcada offline e para de receber pedido. Por isso "último polling
// bem-sucedido" é métrica de negócio, não de infraestrutura.
public interface IPollingHealth
{
    DateTimeOffset? UltimoSucesso { get; }
    DateTimeOffset? UltimaFalha { get; }
    string? UltimoErro { get; }

    void RegistrarSucesso();
    void RegistrarFalha(string erro);
}

internal sealed class PollingHealth : IPollingHealth
{
    private readonly TimeProvider _timeProvider;

    public PollingHealth(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public DateTimeOffset? UltimoSucesso { get; private set; }
    public DateTimeOffset? UltimaFalha { get; private set; }
    public string? UltimoErro { get; private set; }

    public void RegistrarSucesso() => UltimoSucesso = _timeProvider.GetUtcNow();

    public void RegistrarFalha(string erro)
    {
        UltimaFalha = _timeProvider.GetUtcNow();
        UltimoErro = erro;
    }
}
