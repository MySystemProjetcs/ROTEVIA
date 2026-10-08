using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DeliveryHub.Worker.Health;

// Inbox parado é pior que API parada: o pedido já foi reconhecido pelo iFood e
// não vira tela. Este health check cobre os dois caminhos — processador sem
// tick (morreu/travou) e evento mais antigo envelhecendo (fila não drena).
public sealed class InboxLagHealthCheck : IHealthCheck
{
    // Tick do processador é 2s; estes limites dão folga para um lote grande
    // legítimo (100 eventos × HTTP do detalhe do pedido) sem gerar alarme falso.
    private static readonly TimeSpan SemLeituraDegraded = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan SemLeituraUnhealthy = TimeSpan.FromSeconds(180);
    // Evento pendurado: 2min é backlog moroso; 5min é fila que não drena.
    private static readonly TimeSpan IdadeDegraded = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan IdadeUnhealthy = TimeSpan.FromSeconds(300);

    private readonly InboxLagState _estado;
    private readonly TimeProvider _relogio;

    public InboxLagHealthCheck(InboxLagState estado, TimeProvider relogio)
    {
        _estado = estado;
        _relogio = relogio;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var leitura = _estado.Ler();
        var agora = _relogio.GetUtcNow();

        var dados = new Dictionary<string, object>
        {
            ["pendentes"] = leitura.Pendentes,
            ["ultimaLeitura"] = leitura.UltimaLeitura?.ToString("O") ?? "nunca"
        };

        if (leitura.UltimaLeitura is null)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "Processador de Inbox ainda não completou um tick.", data: dados));
        }

        var semLeitura = agora - leitura.UltimaLeitura.Value;
        dados["segundosSemLeitura"] = (int)semLeitura.TotalSeconds;

        if (semLeitura > SemLeituraUnhealthy)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Processador de Inbox sem tick há {(int)semLeitura.TotalSeconds}s — eventos podem estar parados.",
                data: dados));
        }

        if (semLeitura > SemLeituraDegraded)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Processador de Inbox lento: {(int)semLeitura.TotalSeconds}s desde o último tick.",
                data: dados));
        }

        // Sem evento pendente não há o que envelhecer.
        if (leitura.MaisAntigoRecebido is not { } maisAntigo)
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                "Nenhum evento pendente no Inbox.", data: dados));
        }

        var idade = agora - maisAntigo;
        dados["segundosEventoMaisAntigo"] = (int)idade.TotalSeconds;
        dados["maisAntigoRecebido"] = maisAntigo.ToString("O");

        if (idade > IdadeUnhealthy)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Evento mais antigo no Inbox há {(int)idade.TotalSeconds}s — fila não drena (falha repetida ou processador travado).",
                data: dados));
        }

        if (idade > IdadeDegraded)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Backlog do Inbox subindo: evento mais antigo há {(int)idade.TotalSeconds}s.",
                data: dados));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"{leitura.Pendentes} evento(s) pendente(s); o mais antigo há {(int)idade.TotalSeconds}s.",
            data: dados));
    }
}
