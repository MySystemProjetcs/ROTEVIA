import { cn } from '@/lib/cn'
import type { StatusPedido } from '@/dominio/pedido'

// Espelha StatusPedido do backend. Os tokens são semânticos — "estado
// confirmado", não "índigo" —, então trocar a cor de um estado acontece no
// tema e nenhuma tela precisa ser tocada.
const POR_ESTADO: Record<StatusPedido, { rotulo: string; classe: string }> = {
  Recebido: { rotulo: 'Recebido', classe: 'bg-estado-recebido-fundo text-estado-recebido' },
  Confirmado: { rotulo: 'Confirmado', classe: 'bg-estado-confirmado-fundo text-estado-confirmado' },
  EmPreparo: { rotulo: 'Em preparo', classe: 'bg-estado-preparo-fundo text-estado-preparo' },
  Pronto: { rotulo: 'Pronto', classe: 'bg-estado-pronto-fundo text-estado-pronto' },
  Despachado: { rotulo: 'Despachado', classe: 'bg-estado-despachado-fundo text-estado-despachado' },
  Concluido: { rotulo: 'Concluído', classe: 'bg-estado-concluido-fundo text-estado-concluido' },
  Cancelado: { rotulo: 'Cancelado', classe: 'bg-estado-cancelado-fundo text-estado-cancelado' },
}

export function EtiquetaEstado({ estado }: { estado: StatusPedido }) {
  const { rotulo, classe } = POR_ESTADO[estado]

  return (
    <span
      className={cn(
        'inline-flex items-center rounded-controle px-2 py-1 text-rotulo uppercase',
        classe,
      )}
    >
      {rotulo}
    </span>
  )
}

type TomEtiqueta = 'neutro' | 'alerta' | 'sucesso'

const TONS: Record<TomEtiqueta, string> = {
  neutro: 'bg-superficie-afundada text-texto-suave',
  alerta: 'bg-alerta-fundo text-alerta',
  sucesso: 'bg-sucesso-fundo text-sucesso',
}

// Variante genérica, para rótulos que não são estado de pedido — "pedido de
// teste", por exemplo.
export function Etiqueta({ tom = 'neutro', children }: { tom?: TomEtiqueta; children: string }) {
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-controle px-2 py-1 text-rotulo uppercase',
        TONS[tom],
      )}
    >
      {children}
    </span>
  )
}
