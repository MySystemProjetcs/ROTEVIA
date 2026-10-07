import { useEffect, useRef } from 'react'
import type { Pedido } from '@/dominio/pedido'
import { tocarAlertaDePedido } from '@/lib/som'

// Toca o som de chegada (2x) para todo pedido novo. Não há mais aviso de prazo:
// a confirmação é automática na ingestão, então o pedido não fica parado
// esperando o lojista clicar.
export function useAlertaSonoro(pedidos: Pedido[]) {
  const idsConhecidos = useRef<Set<string> | null>(null)

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
}
