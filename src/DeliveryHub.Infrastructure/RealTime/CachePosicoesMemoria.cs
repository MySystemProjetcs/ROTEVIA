using System.Collections.Concurrent;
using DeliveryHub.Application.Abstractions;
using DeliveryHub.Application.Tracking;

namespace DeliveryHub.Infrastructure.RealTime;

// Usado só quando não há Redis configurado. Vale dentro de um processo só —
// que é exatamente o cenário em que se roda sem Redis. Some no restart, como
// qualquer cache.
internal sealed class CachePosicoesMemoria : ICachePosicoes
{
    private static readonly TimeSpan Validade = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<(Guid Merchant, Guid Entregador), (PosicaoRegistrada Posicao, DateTimeOffset Expira)> _posicoes = new();
    private readonly TimeProvider _relogio;

    public CachePosicoesMemoria(TimeProvider relogio)
    {
        _relogio = relogio;
    }

    public Task GravarAsync(PosicaoRegistrada posicao, CancellationToken ct)
    {
        _posicoes[(posicao.MerchantId, posicao.EntregadorId)] =
            (posicao, _relogio.GetUtcNow().Add(Validade));

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PosicaoRegistrada>> ObterDaLojaAsync(Guid merchantId, CancellationToken ct)
    {
        var agora = _relogio.GetUtcNow();

        foreach (var (chave, valor) in _posicoes)
        {
            if (valor.Expira <= agora)
                _posicoes.TryRemove(chave, out _);
        }

        IReadOnlyList<PosicaoRegistrada> resultado = _posicoes
            .Where(x => x.Key.Merchant == merchantId && x.Value.Expira > agora)
            .Select(x => x.Value.Posicao)
            .ToList();

        return Task.FromResult(resultado);
    }
}
