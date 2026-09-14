using DeliveryHub.Application.Tracking;

namespace DeliveryHub.Application.Abstractions;

// Última posição conhecida de cada motoboy, por loja. Existe porque o SignalR
// não tem replay: quem recarrega a página entra no grupo e só recebe o que for
// enviado dali pra frente — sem isso o mapa fica vazio até o próximo ping.
//
// É cache de propósito, com expiração curta, e não tabela: posição de motoboy
// ocioso não vira histórico (a mesma razão pela qual o ping fora de entrega não
// é persistido). Some sozinha quando ele para de emitir.
public interface ICachePosicoes
{
    Task GravarAsync(PosicaoRegistrada posicao, CancellationToken ct);

    Task<IReadOnlyList<PosicaoRegistrada>> ObterDaLojaAsync(Guid merchantId, CancellationToken ct);
}
