using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Merchants;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Application.Merchants;

public interface IDefinirNomeDaLoja
{
    Task<Result> ExecutarAsync(Guid merchantId, string nome, CancellationToken ct);
}

// O nome da loja aparece no painel do dono, no card do pedido do motoboy (que
// atende várias lojas) e no convite do entregador — por isso é edição de
// cadastro, não rótulo de tela.
public sealed class DefinirNomeDaLoja : IDefinirNomeDaLoja
{
    private readonly IMerchantRepository _merchants;
    private readonly INotificadorPainel _notificador;

    public DefinirNomeDaLoja(IMerchantRepository merchants, INotificadorPainel notificador)
    {
        _merchants = merchants;
        _notificador = notificador;
    }

    public async Task<Result> ExecutarAsync(Guid merchantId, string nome, CancellationToken ct)
    {
        var merchant = await _merchants.ObterPorIdAsync(merchantId, ct);
        if (merchant is null)
            return Result.Failure(EnderecoDaLojaErrors.LojaNaoEncontrada);

        var resultado = merchant.AlterarNome(nome);
        if (resultado.IsFailure)
            return resultado;

        await _merchants.SalvarAsync(ct);
        await _notificador.ResumoAtualizadoAsync(merchantId, ct);
        return Result.Success();
    }
}
