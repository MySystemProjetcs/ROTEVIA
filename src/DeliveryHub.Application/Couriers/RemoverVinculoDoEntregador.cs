using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Couriers;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Application.Couriers;

public interface IRemoverVinculoDoEntregador
{
    Task<Result> ExecutarAsync(Guid merchantId, Guid linkId, CancellationToken ct);
}

// Tira o motoboy desta loja. Uma operação só para os dois casos que a tela
// oferece, porque no modelo são a mesma coisa — apagar o vínculo:
//
//   - vínculo Convidado  → revoga o convite pendente (o link do convite morre
//                          junto com o TokenConviteHash, então quem já recebeu
//                          a URL não consegue mais confirmar);
//   - vínculo Ativo      → desvincula o motoboy do restaurante.
//
// O Courier NUNCA é apagado: é identidade global, compartilhada com as outras
// lojas e referenciada pelo histórico de pedidos (CLAUDE.md §6). Sair desta
// loja não pode sumir com a pessoa nem com a entrega que ela já fez.
public sealed class RemoverVinculoDoEntregador : IRemoverVinculoDoEntregador
{
    private readonly ICourierRepository _couriers;
    private readonly IPedidoRepository _pedidos;

    public RemoverVinculoDoEntregador(ICourierRepository couriers, IPedidoRepository pedidos)
    {
        _couriers = couriers;
        _pedidos = pedidos;
    }

    public async Task<Result> ExecutarAsync(Guid merchantId, Guid linkId, CancellationToken ct)
    {
        var link = await _couriers.ObterLinkAsync(linkId, ct);

        if (link is null || link.MerchantId != merchantId)
            return Result.Failure(CourierErrors.VinculoNaoEncontrado);

        // Convite pendente não tem entrega atrelada — só vínculo já ativo
        // precisa da guarda.
        if (link.Status != StatusVinculoEntregador.Convidado
            && await _pedidos.TemEntregaAtivaAsync(link.CourierId, merchantId, ct))
        {
            return Result.Failure(CourierErrors.EntregadorComEntregaAtiva);
        }

        _couriers.RemoverLink(link);
        await _couriers.SalvarAsync(ct);

        return Result.Success();
    }
}
