import { DndContext, DragOverlay, PointerSensor, useSensor, useSensors } from '@dnd-kit/core'
import type { DragEndEvent, DragStartEvent } from '@dnd-kit/core'
import { useState } from 'react'
import { Cartao, CartaoCorpo } from '@/components/Cartao'
import { Etiqueta } from '@/components/Etiqueta'
import type { Pedido, StatusPedido } from '@/dominio/pedido'
import { COLUNAS, podeMoverPara } from '@/dominio/pedido'
import { useAgora } from '@/lib/tempo'
import { CartaoPedido } from './CartaoPedido'
import { ColunaPedidos } from './ColunaPedidos'
import { usePedidos } from './usePedidos'

export function PainelOperacao() {
  const { pedidos, carregando, erro, mover } = usePedidos()
  const agora = useAgora()
  const [arrastando, setArrastando] = useState<Pedido | null>(null)

  // Distância mínima antes de considerar arraste: sem isso, o toque no botão
  // "Confirmar" vira início de arraste e o clique nunca acontece.
  const sensores = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 8 } }))

  function aoIniciar(evento: DragStartEvent) {
    setArrastando(pedidos.find((p) => p.id === evento.active.id) ?? null)
  }

  function aoSoltar(evento: DragEndEvent) {
    setArrastando(null)

    const pedido = pedidos.find((p) => p.id === evento.active.id)
    const destino = evento.over?.id as StatusPedido | undefined

    // A interface só permite o movimento que a máquina de estados aceita: o
    // pedido anda para frente, um passo por vez.
    if (pedido && destino && podeMoverPara(pedido.status, destino)) {
      void mover(pedido, destino)
    }
  }

  if (carregando) {
    return <p className="p-4 text-corpo text-texto-suave">Carregando pedidos…</p>
  }

  return (
    <div className="flex flex-col gap-4">
      {erro && (
        <Cartao className="border-perigo">
          <CartaoCorpo className="flex items-center gap-3">
            <Etiqueta tom="alerta">Erro</Etiqueta>
            <span className="text-corpo text-texto">{erro}</span>
          </CartaoCorpo>
        </Cartao>
      )}

      <DndContext sensors={sensores} onDragStart={aoIniciar} onDragEnd={aoSoltar}>
        <div className="flex gap-4 overflow-x-auto pb-4">
          {COLUNAS.map((estado) => (
            <ColunaPedidos
              key={estado}
              estado={estado}
              pedidos={pedidos.filter((p) => p.status === estado)}
              agora={agora}
              aceitaSolto={arrastando ? podeMoverPara(arrastando.status, estado) : false}
              onAvancar={(pedido) => {
                const destino = COLUNAS[COLUNAS.indexOf(pedido.status) + 1]
                if (destino) void mover(pedido, destino)
              }}
            />
          ))}
        </div>

        {/* O cartão segue o dedo em vez de sumir enquanto arrasta. */}
        <DragOverlay>
          {arrastando && (
            <CartaoPedido pedido={arrastando} agora={agora} onAvancar={() => {}} arrastavel={false} />
          )}
        </DragOverlay>
      </DndContext>
    </div>
  )
}
