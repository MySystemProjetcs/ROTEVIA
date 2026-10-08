using System.Diagnostics.Metrics;

namespace DeliveryHub.Worker.Health;

// Leitura atual do backlog do Inbox, publicada pelo processador a cada tick.
public sealed record LeituraInbox(int Pendentes, DateTimeOffset? MaisAntigoRecebido, DateTimeOffset? UltimaLeitura);

// Estado do backlog em memória, entre o processador (quem escreve) e o health
// check / métricas (quem lê). Uma escrita por tick e leituras de relance — o
// lock é só para os campos não se misturarem entre threads; o custo é
// irrelevante perto de um tick de 2s.
public sealed class InboxLagState
{
    // Gauges vivem aqui (singleton por processo): leem o próprio estado no
    // momento do scrape, sem depender de ninguém lembrar de registrar.
    private static readonly Meter Meter = new("DeliveryHub.Inbox", "1.0.0");

    private readonly TimeProvider _relogio;
    private readonly object _trava = new();
    private int _pendentes;
    private DateTimeOffset? _maisAntigoRecebido;
    private DateTimeOffset? _ultimaLeitura;

    public InboxLagState(TimeProvider relogio)
    {
        _relogio = relogio;

        Meter.CreateObservableGauge("ifood_inbox_pending_count", () => Ler().Pendentes);

        // 0 quando não há fila: gauge nunca deve "sumir" do scrape — sumir e
        // estar zerado são sinais diferentes para quem monitora.
        Meter.CreateObservableGauge("ifood_inbox_oldest_event_age_seconds", () =>
        {
            var leitura = Ler();
            return leitura.MaisAntigoRecebido is { } maisAntigo
                ? Math.Max(0, (_relogio.GetUtcNow() - maisAntigo).TotalSeconds)
                : 0d;
        });
    }

    public void Registrar(int pendentes, DateTimeOffset? maisAntigoRecebido, DateTimeOffset agora)
    {
        lock (_trava)
        {
            _pendentes = pendentes;
            _maisAntigoRecebido = maisAntigoRecebido;
            _ultimaLeitura = agora;
        }
    }

    public LeituraInbox Ler()
    {
        lock (_trava)
        {
            return new LeituraInbox(_pendentes, _maisAntigoRecebido, _ultimaLeitura);
        }
    }
}

