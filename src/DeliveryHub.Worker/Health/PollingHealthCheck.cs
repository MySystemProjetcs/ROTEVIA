using DeliveryHub.Infrastructure.Integrations.IFood.Polling;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DeliveryHub.Worker.Health;

// Sem polling regular o iFood marca a loja como offline e para de mandar
// pedido. Um worker vivo mas travado num ciclo é pior que um worker morto,
// porque não dispara alarme nenhum — por isso o limite olha o horário do
// último sucesso, não se o processo está de pé.
public sealed class PollingHealthCheck : IHealthCheck
{
    // Três ciclos perdidos. Um ciclo falho é ruído de rede; três seguidos é
    // problema de verdade.
    private static readonly TimeSpan LimiteSemSucesso = TimeSpan.FromSeconds(95);

    private readonly IPollingHealth _health;
    private readonly TimeProvider _timeProvider;

    public PollingHealthCheck(IPollingHealth health, TimeProvider timeProvider)
    {
        _health = health;
        _timeProvider = timeProvider;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var ultimoSucesso = _health.UltimoSucesso;

        var dados = new Dictionary<string, object>
        {
            ["ultimoSucesso"] = ultimoSucesso?.ToString("O") ?? "nunca",
            ["ultimaFalha"] = _health.UltimaFalha?.ToString("O") ?? "nunca",
            ["ultimoErro"] = _health.UltimoErro ?? string.Empty
        };

        if (ultimoSucesso is null)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "Nenhum ciclo de polling concluído ainda.", data: dados));
        }

        var desdeOUltimoSucesso = _timeProvider.GetUtcNow() - ultimoSucesso.Value;
        dados["segundosDesdeUltimoSucesso"] = (int)desdeOUltimoSucesso.TotalSeconds;

        return Task.FromResult(desdeOUltimoSucesso > LimiteSemSucesso
            ? HealthCheckResult.Unhealthy(
                $"Sem polling bem-sucedido há {(int)desdeOUltimoSucesso.TotalSeconds}s. As lojas podem estar offline no iFood.",
                data: dados)
            : HealthCheckResult.Healthy("Polling em dia.", dados));
    }
}

