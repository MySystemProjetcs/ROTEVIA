import type { StatusPedido } from '@/dominio/pedido'

// Cromo por estado do pedido: bolinha do título, pill da contagem e fundo da
// coluna do Kanban — e o balão do mapa, que usa o mesmo `ponto` para o pedido
// ter a mesma cor nos dois lugares. Fonte única de propósito: cor divergente
// entre coluna e mapa faria o dono ler o mapa errado.
//
// Classes literais de propósito — o Tailwind só gera o que encontra escrito
// por extenso no código.
export const CROMO_DA_COLUNA: Record<StatusPedido, { ponto: string; pill: string; fundo: string }> = {
  Recebido: {
    ponto: 'bg-estado-recebido',
    pill: 'border-estado-recebido/40 bg-estado-recebido-fundo text-estado-recebido',
    fundo: 'bg-estado-recebido-fundo',
  },
  Confirmado: {
    ponto: 'bg-estado-confirmado',
    pill: 'border-estado-confirmado/40 bg-estado-confirmado-fundo text-estado-confirmado',
    fundo: 'bg-estado-confirmado-fundo',
  },
  EmPreparo: {
    ponto: 'bg-estado-preparo',
    pill: 'border-estado-preparo/40 bg-estado-preparo-fundo text-estado-preparo',
    fundo: 'bg-estado-preparo-fundo',
  },
  Pronto: {
    ponto: 'bg-estado-pronto',
    pill: 'border-estado-pronto/40 bg-estado-pronto-fundo text-estado-pronto',
    fundo: 'bg-estado-pronto-fundo',
  },
  Despachado: {
    ponto: 'bg-estado-despachado',
    pill: 'border-estado-despachado/40 bg-estado-despachado-fundo text-estado-despachado',
    fundo: 'bg-estado-despachado-fundo',
  },
  Aceito: {
    ponto: 'bg-estado-aceito',
    pill: 'border-estado-aceito/40 bg-estado-aceito-fundo text-estado-aceito',
    fundo: 'bg-estado-aceito-fundo',
  },
  EmRota: {
    ponto: 'bg-estado-emrota',
    pill: 'border-estado-emrota/40 bg-estado-emrota-fundo text-estado-emrota',
    fundo: 'bg-estado-emrota-fundo',
  },
  Chegou: {
    ponto: 'bg-estado-chegou',
    pill: 'border-estado-chegou/40 bg-estado-chegou-fundo text-estado-chegou',
    fundo: 'bg-estado-chegou-fundo',
  },
  Cobrar: {
    ponto: 'bg-estado-cobrar',
    pill: 'border-estado-cobrar/40 bg-estado-cobrar-fundo text-estado-cobrar',
    fundo: 'bg-estado-cobrar-fundo',
  },
  Concluido: {
    ponto: 'bg-estado-concluido',
    pill: 'border-estado-concluido/40 bg-estado-concluido-fundo text-estado-concluido',
    fundo: 'bg-estado-concluido-fundo',
  },
  Cancelado: {
    ponto: 'bg-estado-cancelado',
    pill: 'border-estado-cancelado/40 bg-estado-cancelado-fundo text-estado-cancelado',
    fundo: 'bg-estado-cancelado-fundo',
  },
}
