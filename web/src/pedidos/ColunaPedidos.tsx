import { useDroppable } from '@dnd-kit/core'
import { SortableContext, verticalListSortingStrategy } from '@dnd-kit/sortable'
import type { Entregador } from '@/dominio/entregador'
import type { ColunaDoQuadro, ObterProximoPasso, Pedido } from '@/dominio/pedido'
import { cn } from '@/lib/cn'
import { CartaoPedido } from './CartaoPedido'
import { CROMO_DA_COLUNA } from './cromoDoStatus'
import type { PosicaoEntregador } from './useRastreio'

interface ColunaPedidosProps {
  coluna: ColunaDoQuadro
  pedidos: Pedido[]
  agora: number
  aceitaSolto: boolean
  onAvancar: (pedido: Pedido) => void
  obterProximoPasso: ObterProximoPasso
  entregadoresAtivos?: Entregador[]
  onAlocar?: (pedido: Pedido, entregadorId: string) => void
  onCancelar?: (pedido: Pedido, motivo: string) => Promise<string | null>
  posicoes?: PosicaoEntregador[]
}

export function ColunaPedidos({
  coluna,
  pedidos,
  agora,
  aceitaSolto,
  onAvancar,
  obterProximoPasso,
  entregadoresAtivos,
  onAlocar,
  onCancelar,
  posicoes = [],
}: ColunaPedidosProps) {
  // Coluna sem destino não recebe cartão: o dnd-kit só a registra como alvo se
  // houver para onde mover.
  const { setNodeRef, isOver } = useDroppable({ id: coluna.id, disabled: !coluna.destino })
  const cromo = CROMO_DA_COLUNA[coluna.cor]

  return (
    // Divide a largura com as irmãs em vez de largura fixa: com sete colunas,
    // `w-72` somava mais que a tela e o navegador criava a barra horizontal.
    // `min-w-0` é o que permite encolher — item de flex não passa do conteúdo
    // sem isso. O piso de 11rem é onde o cartão ainda se lê.
    <section className="flex min-w-0 flex-1 basis-44 flex-col gap-2">
      <header className="flex items-center gap-2 px-1">
        <span aria-hidden className={cn('size-2.5 shrink-0 rounded-full', cromo.ponto)} />
        <h2 className="flex-1 text-apoio font-semibold text-texto">{coluna.titulo}</h2>
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
          // Rolagem vertical na própria coluna, não num embrulho em volta: o
          // fundo é pintado na caixa do elemento que rola, então ele fica
          // parado enquanto os cartões correm por dentro.
          //
          // `overflow-x-hidden` é obrigatório, não enfeite: com só
          // `overflow-y-auto`, o CSS computa o outro eixo como `auto` também e
          // aparece uma barra horizontal que ninguém pediu.
          coluna.rolavel && 'barra-fina max-h-[28rem] overflow-y-auto overflow-x-hidden',
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
              onCancelar={onCancelar}
              posicoes={posicoes}
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
