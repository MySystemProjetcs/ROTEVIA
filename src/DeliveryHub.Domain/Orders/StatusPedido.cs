namespace DeliveryHub.Domain.Orders;

// A ordem dos valores é significativa: o polling do iFood não garante ordem de
// entrega dos eventos, e a máquina de estados usa essa sequência para nunca
// retroceder o status quando um evento atrasado chega.
public enum StatusPedido
{
    Recebido = 0,
    Confirmado = 1,
    EmPreparo = 2,
    Pronto = 3,
    Despachado = 4,
    Concluido = 5,

    // Fora da linha de progressão: alcançável de qualquer estado não terminal.
    Cancelado = 99
}
