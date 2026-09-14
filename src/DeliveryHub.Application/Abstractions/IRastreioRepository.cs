using DeliveryHub.Domain.Tracking;

namespace DeliveryHub.Application.Abstractions;

public interface IRastreioRepository
{
    void Adicionar(PosicaoEntregador posicao);

    Task SalvarAsync(CancellationToken ct);
}
