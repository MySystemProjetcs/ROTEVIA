using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Couriers;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Application.Couriers;

public interface IAtualizarCadastroDoEntregador
{
    Task<Result> ExecutarAsync(
        Guid merchantId, Guid linkId, string nome, string telefone,
        string modeloDaMoto, string placa, CancellationToken ct);
}

// Correção do cadastro do motoboy pelo restaurante. Endereçado pelo LinkId, não
// pelo CourierId: é o vínculo que prova que este motoboy é desta loja — receber
// o CourierId direto deixaria qualquer loja editar qualquer entregador.
//
// O que muda é o Courier, que é global (CLAUDE.md §6), então a correção vale
// para todas as lojas que trabalham com ele. É o preço de não duplicar
// cadastro, e é o comportamento certo: nome e placa são da pessoa, não da loja.
public sealed class AtualizarCadastroDoEntregador : IAtualizarCadastroDoEntregador
{
    private readonly ICourierRepository _couriers;

    public AtualizarCadastroDoEntregador(ICourierRepository couriers)
    {
        _couriers = couriers;
    }

    public async Task<Result> ExecutarAsync(
        Guid merchantId, Guid linkId, string nome, string telefone,
        string modeloDaMoto, string placa, CancellationToken ct)
    {
        var link = await _couriers.ObterLinkAsync(linkId, ct);

        if (link is null || link.MerchantId != merchantId)
            return Result.Failure(CourierErrors.VinculoNaoEncontrado);

        var courier = await _couriers.ObterPorIdAsync(link.CourierId, ct);
        if (courier is null)
            return Result.Failure(CourierErrors.VinculoNaoEncontrado);

        var resultado = courier.AtualizarCadastro(nome, telefone, modeloDaMoto, placa);
        if (resultado.IsFailure)
            return resultado;

        await _couriers.SalvarAsync(ct);
        return Result.Success();
    }
}
