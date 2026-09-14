using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Domain.Tracking;

public static class TrackingErrors
{
    public static readonly Error EntregadorNaoEncontrado = new(
        "rastreio.entregador_nao_encontrado",
        "Entregador não encontrado.",
        ErrorType.NotFound);

    public static readonly Error RastreioForaDeTurno = new(
        "rastreio.fora_de_turno",
        "Só há rastreio com o entregador online ou em entrega.",
        ErrorType.Conflict);
}
