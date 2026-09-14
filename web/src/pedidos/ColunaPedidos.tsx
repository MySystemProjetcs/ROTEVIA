import { useDroppable } from '@dnd-kit/core'
import { SortableContext, verticalListSortingStrategy } from '@dnd-kit/sortable'
import type { Entregador } from '@/dominio/entregador'
import type { ObterProximoPasso, Pedido, StatusPedido } from '@/dominio/pedido'
import { TITULO_COLUNA } from '@/dominio/pedido'
import { cn } from '@/lib/cn'
import { CartaoPedido } from './CartaoPedido'
import type { PosicaoEntregador } from './useRastreio'

// Cromo da coluna por estado: bolinha do título, pill da contagem e fundo da
// coluna. Classes literais de propósito — o Tailwind só gera o que encontra
// escrito por extenso no código.
const CROMO_DA_COLUNA: Record<StatusPedido, { ponto: string; pill: string; fundo: string }> = {
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

interface ColunaPedidosProps {
  estado: StatusPedido
  pedidos: Pedido[]
  agora: number
  aceitaSolto: boolean
  onAvancar: (pedido: Pedido) => void
  obterProximoPasso: ObterProximoPasso
  entregadoresAtivos?: Entregador[]
  onAlocar?: (pedido: Pedido, entregadorId: string) => void
  posicoes?: PosicaoEntregador[]
  trilhaRastreio?: PosicaoEntregador[]
}

export function ColunaPedidos({
  estado,
  pedidos,
  agora,
  aceitaSolto,
  onAvancar,
  obterProximoPasso,
  entregadoresAtivos,
  onAlocar,
  posicoes = [],
  trilhaRastreio = [],
}: ColunaPedidosProps) {
  const { setNodeRef, isOver } = useDroppable({ id: estado })
  const cromo = CROMO_DA_COLUNA[estado]

  return (
    <section className="flex w-72 shrink-0 flex-col gap-2">
      <header className="flex items-center gap-2 px-1">
        <span aria-hidden className={cn('size-2.5 shrink-0 rounded-full', cromo.ponto)} />
        <h2 className="flex-1 text-apoio font-semibold text-texto">{TITULO_COLUNA[estado]}</h2>
        <span
          aria-label={`${pedidos.length} pedidos`}
          className={cn(
            'inline-flex min-w-6 items-center justify-center rounded-full border px-1.5 py-0.5 text-apoio font-semibold tabular-nums',
            cromo.pill,
          )}
        >
          {pedidos.length}
        </span>
      </header>

      <div
        ref={setNodeRef}
        className={cn(
          'flex min-h-40 flex-col gap-3 rounded-cartao p-2 transition-colors',
          cromo.fundo,
          // Só destaca quando o movimento é válido: destacar coluna que vai
          // recusar o pedido ensina o gesto errado.
          isOver && aceitaSolto && 'ring-2 ring-marca-400 ring-inset',
        )}
      >
        <SortableContext items={pedidos.map((p) => p.id)} strategy={verticalListSortingStrategy}>
          {pedidos.map((pedido) => (
            <CartaoPedido
              key={pedido.id}
              pedido={pedido}
              agora={agora}
              onAvancar={onAvancar}
              obterProximoPasso={obterProximoPasso}
              entregadoresAtivos={entregadoresAtivos}
              onAlocar={onAlocar}
              posicoes={posicoes}
              trilhaRastreio={trilhaRastreio}
            />
          ))}
        </SortableContext>

        {pedidos.length === 0 && (
          <p className="px-2 py-6 text-center text-apoio text-texto-fraco">Nenhum pedido aqui.</p>
        )}
      </div>
    </section>
  )
}
