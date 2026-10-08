using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace DeliveryHub.Worker;

// Métricas de polling (plano §5): duração de ciclo, sucesso/falha por loja e
// timestamp do último sucesso — o "heartbeat real" por merchant, que o
// PollingHealthCheck global não mostra. Sem exporter OTLP/Prometheus hoje os
// instrumentos ficam registrados e inertes; ligar o export é configuração de
// infra, não muda estes nomes.
internal static class MetricasPolling
{
    private static readonly Meter Meter = new("DeliveryHub.Polling", "1.0.0");

    private static readonly Histogram<double> DuracaoCiclo = Meter.CreateHistogram<double>(
        "ifood_polling_cycle_duration_seconds", "s",
        "Duração de um ciclo completo de polling (lojas distribuídas + centralizado).");

    private static readonly Counter<long> Sucesso = Meter.CreateCounter<long>(
        "ifood_polling_success_total",
        description: "Pollings de loja concluídos com sucesso, por merchant.");

    private static readonly Counter<long> Falha = Meter.CreateCounter<long>(
        "ifood_polling_failure_total",
        description: "Pollings de loja que falharam (erro, timeout ou circuito aberto), por merchant.");

    private static readonly ConcurrentDictionary<Guid, double> UltimoSucesso = new();

    static MetricasPolling()
    {
        // Unix seconds para o gauge casar com a convenção de timestamps;
        // merchant_id como tag mantém a cardinalidade presa ao número de lojas.
        Meter.CreateObservableGauge("ifood_last_successful_poll_timestamp", () =>
            UltimoSucesso.Select(kv => new Measurement<double>(
                kv.Value,
                new KeyValuePair<string, object?>("merchant_id", kv.Key.ToString()))));
    }

    // Só para forçar a inicialização do tipo (e o registro do gauge) no boot,
    // antes do primeiro ciclo — senão o gauge só existiria após um poll.
    public static void Inicializar()
    {
    }

    public static void RegistrarDuracaoDoCiclo(TimeSpan duracao) =>
        DuracaoCiclo.Record(duracao.TotalSeconds);

    public static void RegistrarSucesso(Guid merchantId)
    {
        Sucesso.Add(1, new KeyValuePair<string, object?>("merchant_id", merchantId.ToString()));
        UltimoSucesso[merchantId] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public static void RegistrarFalha(Guid merchantId) =>
        Falha.Add(1, new KeyValuePair<string, object?>("merchant_id", merchantId.ToString()));
}
