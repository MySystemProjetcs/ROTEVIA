import { useSortable } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { Botao } from '@/components/Botao'
import { Cartao, CartaoCorpo, CartaoRodape } from '@/components/Cartao'
import { Etiqueta, EtiquetaEstado } from '@/components/Etiqueta'
import type { Pedido } from '@/dominio/pedido'
import { proximoPasso } from '@/dominio/pedido'
import { cn } from '@/lib/cn'
import { formatarDinheiro } from '@/lib/tempo'
import { ContadorSla, TempoDecorrido } from './ContadorSla'

interface CartaoPedidoProps {
  pedido: Pedido
  agora: number
  onAvancar: (pedido: Pedido) => void
  arrastavel?: boolean
}

export function CartaoPedido({ pedido, agora, onAvancar, arrastavel = true }: CartaoPedidoProps) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: pedido.id,
    disabled: !arrastavel,
  })

  const passo = proximoPasso(pedido.status)

  return (
    <Cartao
      ref={setNodeRef}
      style={{ transform: CSS.Translate.toString(transform), transition }}
      className={cn(isDragging && 'opacity-40')}
    >
      {/* O arraste vive no corpo, não no cartão inteiro: com os listeners no
          elemento externo, o sensor engolia o clique do botão do rodapé — e o
          cartão ainda ficava com role="button" envolvendo um botão de verdade. */}
      <CartaoCorpo
        {...attributes}
        {...listeners}
        className={cn('flex flex-col gap-3', arrastavel && 'cursor-grab touch-none active:cursor-grabbing')}
      >
        <div className="flex items-start justify-between gap-2">
          <div>
            <span className="text-titulo text-texto">#{pedido.numeroExibicao}</span>
            <p className="text-apoio text-texto-suave">{pedido.clienteNome}</p>
          </div>
          <EtiquetaEstado estado={pedido.status} />
        </div>

        {pedido.status === 'Recebido' ? (
          <ContadorSla prazoAte={pedido.prazoConfirmacaoAte} agora={agora} />
        ) : (
          <TempoDecorrido desde={pedido.recebidoEm} agora={agora} rotulo="Na loja há" />
        )}

        <ul className="flex flex-col gap-1">
          {pedido.itens.map((item) => (
            <li key={item.indice} className="flex justify-between gap-2 text-apoio text-texto">
              <span>
                <span className="font-medium">{item.quantidade}×</span> {item.nome}
              </span>
              <span className="shrink-0 text-texto-suave">{formatarDinheiro(item.precoTotal)}</span>
            </li>
          ))}
        </ul>

        <div className="flex items-center justify-between gap-2 border-t border-borda pt-2">
          <span className="text-apoio text-texto-suave">
            {pedido.itens.length} {pedido.itens.length === 1 ? 'item' : 'itens'}
          </span>
          <span className="text-corpo font-semibold text-texto">
            {formatarDinheiro(pedido.valorTotal)}
          </span>
        </div>

        {pedido.ehTeste && <Etiqueta tom="alerta">Pedido de teste</Etiqueta>}
      </CartaoCorpo>

      {passo && (
        <CartaoRodape>
          <Botao variante="primario" larguraTotal onClick={() => onAvancar(pedido)}>
            {passo.rotulo}
          </Botao>
        </CartaoRodape>
      )}
    </Cartao>
  )
}
