using System.Text.Json;
using DeliveryHub.Application.Abstractions;
using DeliveryHub.Application.Tracking;
using StackExchange.Redis;

namespace DeliveryHub.Infrastructure.RealTime;

internal sealed class CachePosicoesRedis : ICachePosicoes
{
    // Cinco minutos aguentam a aba do motoboy estrangulada em segundo plano e
    // túnel sem sinal, e ainda limpam em tempo razoável quem saiu de verdade.
    private static readonly TimeSpan Validade = TimeSpan.FromMinutes(5);

    private readonly IConnectionMultiplexer _redis;

    public CachePosicoesRedis(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    // A posição de cada motoboy é uma chave própria, porque só chave tem TTL
    // individual — campo de hash não tem. O conjunto ao lado é só o índice de
    // quem consultar, e é limpo preguiçosamente quando a chave já expirou.
    private static string ChaveDaPosicao(Guid merchantId, Guid entregadorId) =>
        $"rastreio:pos:{merchantId}:{entregadorId}";

    private static string ChaveDoIndice(Guid merchantId) => $"rastreio:loja:{merchantId}";

    public async Task GravarAsync(PosicaoRegistrada posicao, CancellationToken ct)
    {
        var db = _redis.GetDatabase();

        await db.StringSetAsync(
            ChaveDaPosicao(posicao.MerchantId, posicao.EntregadorId),
            JsonSerializer.Serialize(posicao),
            Validade);

        await db.SetAddAsync(ChaveDoIndice(posicao.MerchantId), posicao.EntregadorId.ToString());
    }

    public async Task<IReadOnlyList<PosicaoRegistrada>> ObterDaLojaAsync(Guid merchantId, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var indice = ChaveDoIndice(merchantId);
        var ids = await db.SetMembersAsync(indice);

        var posicoes = new List<PosicaoRegistrada>(ids.Length);

        foreach (var id in ids)
        {
            if (!Guid.TryParse(id.ToString(), out var entregadorId))
                continue;

            var bruto = await db.StringGetAsync(ChaveDaPosicao(merchantId, entregadorId));

            if (bruto.IsNullOrEmpty)
            {
                // Expirou: tira do índice para ele não crescer indefinidamente.
                await db.SetRemoveAsync(indice, id);
                continue;
            }

            var posicao = JsonSerializer.Deserialize<PosicaoRegistrada>(bruto!);
            if (posicao is not null)
                posicoes.Add(posicao);
        }

        return posicoes;
    }
}
