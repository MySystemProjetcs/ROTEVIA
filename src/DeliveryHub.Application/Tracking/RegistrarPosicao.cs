using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.SharedKernel;
using DeliveryHub.Domain.Tracking;

namespace DeliveryHub.Application.Tracking;

// O que a loja recebe no mapa. Um por merchant: o motoboy pode estar ativo em
// mais de um restaurante, e cada um recebe no seu próprio grupo.
//
// Os campos do pedido vêm nulos quando o motoboy está online sem entrega — é o
// que permite a loja vê-lo parado no mapa, sem pedido associado.
public sealed record PosicaoRegistrada(
    Guid MerchantId,
    Guid EntregadorId,
    string EntregadorNome,
    double Latitude,
    double Longitude,
    double PrecisaoEmMetros,
    DateTimeOffset CapturadoEm,
    Guid? PedidoId,
    string? PedidoNumero,
    string? PedidoStatus,
    string? ClienteNome,
    string? EnderecoResumido);

public interface IRegistrarPosicao
{
    // Sem pedidoId: quem descobre se há entrega em curso é o servidor, a partir
    // do próprio motoboy. O app só informa onde ele está.
    Task<Result<IReadOnlyList<PosicaoRegistrada>>> ExecutarAsync(
        Guid usuarioLogadoId,
        double latitude,
        double longitude,
        double precisaoEmMetros,
        DateTimeOffset capturadoEm,
        CancellationToken ct);
}

public sealed class RegistrarPosicao : IRegistrarPosicao
{
    private readonly IPedidoRepository _pedidos;
    private readonly ICourierRepository _couriers;
    private readonly IRastreioRepository _rastreio;
    private readonly ICachePosicoes _cache;
    private readonly TimeProvider _relogio;

    public RegistrarPosicao(
        IPedidoRepository pedidos,
        ICourierRepository couriers,
        IRastreioRepository rastreio,
        ICachePosicoes cache,
        TimeProvider relogio)
    {
        _pedidos = pedidos;
        _couriers = couriers;
        _rastreio = rastreio;
        _cache = cache;
        _relogio = relogio;
    }

    public async Task<Result<IReadOnlyList<PosicaoRegistrada>>> ExecutarAsync(
        Guid usuarioLogadoId,
        double latitude,
        double longitude,
        double precisaoEmMetros,
        DateTimeOffset capturadoEm,
        CancellationToken ct)
    {
        var courier = await _couriers.ObterPorUsuarioIdAsync(usuarioLogadoId, ct);
        if (courier is null)
            return Result.Failure<IReadOnlyList<PosicaoRegistrada>>(TrackingErrors.EntregadorNaoEncontrado);

        var entrega = await _pedidos.ObterEntregaEmCursoAsync(courier.Id, ct);

        // Offline e sem entrega em curso não se rastreia. O alternador
        // Online/Offline é o consentimento do motoboy: fora do turno, a
        // localização dele não é assunto de ninguém (LGPD, minimização).
        if (entrega is null && !courier.DisponivelParaEntrega)
            return Result.Failure<IReadOnlyList<PosicaoRegistrada>>(TrackingErrors.RastreioForaDeTurno);

        if (entrega is not null)
        {
            // Durante a entrega o trajeto é gravado: serve de prova do que foi
            // percorrido se o pedido for contestado depois.
            _rastreio.Adicionar(PosicaoEntregador.Registrar(
                merchantId: entrega.MerchantId,
                entregadorId: courier.Id,
                pedidoId: entrega.Id,
                latitude: latitude,
                longitude: longitude,
                precisaoEmMetros: precisaoEmMetros,
                capturadoEm: capturadoEm,
                recebidoEm: _relogio.GetUtcNow()));

            await _rastreio.SalvarAsync(ct);

            var daEntrega = new PosicaoRegistrada(
                entrega.MerchantId, courier.Id, courier.Nome,
                latitude, longitude, precisaoEmMetros, capturadoEm,
                entrega.Id,
                entrega.NumeroExibicao,
                entrega.Status.ToString(),
                entrega.Cliente.Nome,
                Resumir(entrega.EnderecoEntrega));

            await _cache.GravarAsync(daEntrega, ct);

            return Result.Success<IReadOnlyList<PosicaoRegistrada>>([daEntrega]);
        }

        // Online sem entrega: transmite ao vivo para as lojas dele, mas não
        // grava. Histórico de onde a pessoa esteve enquanto esperava pedido não
        // tem uso de negócio — seria só vigilância acumulada.
        var merchantIds = await _couriers.ListarMerchantIdsAtivosAsync(courier.Id, ct);

        var ociosas = merchantIds
            .Select(merchantId => new PosicaoRegistrada(
                merchantId, courier.Id, courier.Nome,
                latitude, longitude, precisaoEmMetros, capturadoEm,
                null, null, null, null, null))
            .ToList();

        // No cache sim, na tabela não: é o que permite o mapa se recompor
        // depois de um F5 sem criar histórico permanente de gente ociosa.
        foreach (var posicao in ociosas)
            await _cache.GravarAsync(posicao, ct);

        return Result.Success<IReadOnlyList<PosicaoRegistrada>>(ociosas);
    }

    private static string? Resumir(Endereco? endereco) =>
        endereco is null
            ? null
            : $"{endereco.Logradouro}, {endereco.Numero} - {endereco.Bairro}";
}
