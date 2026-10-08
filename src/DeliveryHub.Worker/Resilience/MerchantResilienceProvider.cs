using System.Collections.Concurrent;
using DeliveryHub.Infrastructure.Integrations.IFood;
using DeliveryHub.Infrastructure.Integrations.IFood.Auth;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace DeliveryHub.Worker.Resilience;

// Parâmetros do pipeline por loja. Em options próprias (não IOptions) porque
// os testes precisam encurtar timeout e cooldown sem depender de configuração.
public sealed record MerchantResilienceOptions
{
    // 8s < ciclo de 8s: uma loja travada não pode segurar o ciclo inteiro.
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(8);
    // Depois de abrir, o circuito fica 2min sem chamar a loja — repetir não
    // resolve falha de API, só gasta o rate limit do iFood.
    public TimeSpan BreakDuration { get; init; } = TimeSpan.FromMinutes(2);
    public double FailureRatio { get; init; } = 0.5;
    // Piso de 4 amostras: com menos, um soluço de rede isolado abre o circuito.
    public int MinimumThroughput { get; init; } = 4;
    public TimeSpan SamplingDuration { get; init; } = TimeSpan.FromSeconds(30);
}

// Pipeline de resiliência por merchant: timeout curto + circuit breaker,
// guardado em ConcurrentDictionary<Guid, ResiliencePipeline> (chave =
// MerchantId) — cada loja tem seu próprio raio de falha, como manda o
// isolamento do polling.
public sealed class MerchantResilienceProvider
{
    private readonly ConcurrentDictionary<Guid, ResiliencePipeline> _porLoja = new();
    private readonly MerchantResilienceOptions _opcoes;

    public MerchantResilienceProvider(MerchantResilienceOptions opcoes)
    {
        _opcoes = opcoes;
    }

    public Task<T> ExecutarAsync<T>(Guid merchantId, CancellationToken ct, Func<CancellationToken, Task<T>> acao)
    {
        var pipeline = _porLoja.GetOrAdd(merchantId, _ => Construir());
        return pipeline.ExecuteAsync(async token => await acao(token), ct).AsTask();
    }

    private ResiliencePipeline Construir() =>
        new ResiliencePipelineBuilder()
            // Breaker PRIMEIRO (por fora): a Polly executa as estratégias na
            // ordem em que são adicionadas. Com o breaker por fora, o
            // TimeoutRejectedException do timeout interno atravessa o breaker e
            // conta como falha. Na ordem inversa (timeout por fora) o breaker só
            // veria OperationCanceledException — que ele ignora — e uma loja que
            // só travava nunca abriria o circuito.
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = _opcoes.FailureRatio,
                MinimumThroughput = _opcoes.MinimumThroughput,
                SamplingDuration = _opcoes.SamplingDuration,
                BreakDuration = _opcoes.BreakDuration,
                ShouldHandle = new PredicateBuilder()
                    .Handle<IFoodApiException>()
                    .Handle<IFoodAuthenticationException>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>()
            })
            .AddTimeout(_opcoes.Timeout)
            .Build();
}
