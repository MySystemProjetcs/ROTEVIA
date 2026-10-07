using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Domain.Couriers;

public static class CourierErrors
{
    public static readonly Error ConviteExpirado = new(
        "courier.convite_expirado",
        "O convite expirou. Peça ao restaurante para gerar um novo.",
        ErrorType.Conflict);

    public static readonly Error ConviteInvalido = new(
        "courier.convite_invalido",
        "Este convite não é mais válido.",
        ErrorType.Conflict);

    public static readonly Error EntregadorJaVinculado = new(
        "courier.entregador_ja_vinculado",
        "Este entregador já está convidado ou ativo nesta loja.",
        ErrorType.Conflict);

    public static readonly Error DadosInvalidos = new(
        "courier.dados_invalidos",
        "Preencha nome, telefone, modelo da moto e placa.",
        ErrorType.Validation);

    public static readonly Error EntregadorComEntregaAtiva = new(
        "courier.entrega_ativa",
        "Este motoboy tem entrega em andamento. Conclua ou reatribua antes de removê-lo.",
        ErrorType.Conflict);

    public static readonly Error VinculoNaoEncontrado = new(
        "courier.vinculo_nao_encontrado",
        "Convite não encontrado.",
        ErrorType.NotFound);
}
