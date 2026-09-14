using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Application.Merchants;

public interface IDefinirTaxaPorEntrega
{
    Task<Result<decimal>> ExecutarAsync(Guid merchantId, decimal valor, CancellationToken ct);
}

// O valor que a loja paga ao motoboy por entrega concluída. Vale dali pra
// frente: o ganho de cada pedido é o snapshot da taxa na conclusão, então
// reajuste nunca reescreve histórico.
public sealed class DefinirTaxaPorEntrega : IDefinirTaxaPorEntrega
{
    private readonly IMerchantRepository _merchants;
    private readonly INotificadorPainel _notificador;

    public DefinirTaxaPorEntrega(IMerchantRepository merchants, INotificadorPainel notificador)
    {
        _merchants = merchants;
        _notificador = notificador;
    }

    public async Task<Result<decimal>> ExecutarAsync(Guid merchantId, decimal valor, CancellationToken ct)
    {
        var merchant = await _merchants.ObterPorIdAsync(merchantId, ct);
        if (merchant is null)
            return Result.Failure<decimal>(ConexaoIFoodErrors.MerchantNaoEncontrado);

        var definido = merchant.DefinirTaxaPorEntrega(valor);
        if (definido.IsFailure)
            return Result.Failure<decimal>(definido.Error);

        await _merchants.SalvarAsync(ct);
        await _notificador.ResumoAtualizadoAsync(merchantId, ct);

        return Result.Success(merchant.TaxaPadraoPorEntrega);
    }
}
