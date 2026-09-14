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

    // Motoboy interno já foi alocado e o dono despachou — aguardando o
    // motoboy aceitar. Concluido sobe de valor pra abrir espaço aqui; seguro
    // porque o status é persistido como string, não como número.
    Despachado = 4,
    Aceito = 5,
    EmRota = 6,
    Chegou = 7,
    Concluido = 8,

    // Fora da linha de progressão: alcançável de qualquer estado não terminal.
    Cancelado = 99
}
