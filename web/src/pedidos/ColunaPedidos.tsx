import { useDroppable } from '@dnd-kit/core'
import { SortableContext, verticalListSortingStrategy } from '@dnd-kit/sortable'
import type { Pedido, StatusPedido } from '@/dominio/pedido'
import { TITULO_COLUNA } from '@/dominio/pedido'
import { cn } from '@/lib/cn'
import { CartaoPedido } from './CartaoPedido'

// Cada coluna tem a cor do estado que representa, puxada do token semântico —
// é o que dá a leitura de relance que o painel precisa.
const FAIXA_DA_COLUNA: Record<StatusPedido, string> = {
  Recebido: 'bg-estado-recebido',
  Confirmado: 'bg-estado-confirmado',
  EmPreparo: 'bg-estado-preparo',
  Pronto: 'bg-estado-pronto',
  Despachado: 'bg-estado-despachado',
  Concluido: 'bg-estado-concluido',
  Cancelado: 'bg-estado-cancelado',
}

interface ColunaPedidosProps {
  estado: StatusPedido
  pedidos: Pedido[]
  agora: number
  aceitaSolto: boolean
  onAvancar: (pedido: Pedido) => void
}

export function ColunaPedidos({ estado, pedidos, agora, aceitaSolto, onAvancar }: ColunaPedidosProps) {
  const { setNodeRef, isOver } = useDroppable({ id: estado })

  return (
    <section className="flex w-72 shrink-0 flex-col gap-3">
      <header className="flex flex-col gap-2">
        <span className={cn('h-1 w-full rounded-controle', FAIXA_DA_COLUNA[estado])} />
        <div className="flex items-baseline justify-between gap-2">
          <h2 className="text-apoio font-semibold text-texto">{TITULO_COLUNA[estado]}</h2>
          <span className="text-apoio text-texto-fraco">{pedidos.length}</span>
        </div>
      </header>

      <div
        ref={setNodeRef}
        className={cn(
          'flex min-h-40 flex-col gap-3 rounded-cartao border border-dashed p-2 transition-colors',
          // Só destaca quando o movimento é válido: destacar coluna que vai
          // recusar o pedido ensina o gesto errado.
          isOver && aceitaSolto ? 'border-marca-400 bg-marca-50' : 'border-transparent',
        )}
      >
        <SortableContext items={pedidos.map((p) => p.id)} strategy={verticalListSortingStrategy}>
          {pedidos.map((pedido) => (
            <CartaoPedido key={pedido.id} pedido={pedido} agora={agora} onAvancar={onAvancar} />
          ))}
        </SortableContext>

        {pedidos.length === 0 && (
          <p className="px-2 py-6 text-center text-apoio text-texto-fraco">Nenhum pedido aqui.</p>
        )}
      </div>
    </section>
  )
}
