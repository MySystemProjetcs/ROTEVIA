import { useEffect, useRef } from 'react'
import type { Pedido } from '@/dominio/pedido'
import { tocarAlertaDePedido } from '@/lib/som'

// Avisa de novo quando falta 1 min pro prazo de 8 min de confirmação do iFood.
const JANELA_AVISO_MS = 60_000

// Toca o som de chegada (2x) pra todo pedido novo, e um aviso extra (1x)
// quando um pedido "Recebido" está a 1 min de estourar o prazo. Ambos vivem no
// mesmo hook porque compartilham a mesma fonte de dados (pedidos + relógio) e
// o mesmo cuidado de não repetir o alerta.
export function useAlertaSonoro(pedidos: Pedido[], agora: number) {
  const idsConhecidos = useRef<Set<string> | null>(null)
  const avisados1Min = useRef<Set<string>>(new Set())

  useEffect(() => {
    // Primeira carga da tela: só estabelece a base. Sem isso, todo pedido já
    // ativo tocaria o alerta de "chegou" assim que a página abrisse.
    if (idsConhecidos.current === null) {
      idsConhecidos.current = new Set(pedidos.map((p) => p.id))
      return
    }

    for (const pedido of pedidos) {
      if (idsConhecidos.current.has(pedido.id)) continue

      idsConhecidos.current.add(pedido.id)
      void tocarAlertaDePedido(2)
    }
  }, [pedidos])

  useEffect(() => {
    for (const pedido of pedidos) {
      if (pedido.status !== 'Recebido' || avisados1Min.current.has(pedido.id)) continue

      const restanteMs = new Date(pedido.prazoConfirmacaoAte).getTime() - agora
      if (restanteMs <= JANELA_AVISO_MS) {
        avisados1Min.current.add(pedido.id)
        void tocarAlertaDePedido(1)
      }
    }
  }, [pedidos, agora])
}
