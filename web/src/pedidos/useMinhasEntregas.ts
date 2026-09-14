import { useCallback, useEffect, useState } from 'react'
import type { Pedido, StatusPedido } from '@/dominio/pedido'
import { acaoParaEntregador } from '@/dominio/pedido'
import { api, ErroDaApi } from '@/lib/api'

const INTERVALO_ATUALIZACAO_MS = 4_000

// Espelha usePedidos, lado do motoboy: mesmo formato de retorno, endpoints
// diferentes (/api/entregador/...) e ações da máquina de estados do entregador.
export function useMinhasEntregas() {
  const [pedidos, setPedidos] = useState<Pedido[]>([])
  const [carregando, setCarregando] = useState(true)
  const [erro, setErro] = useState<string | null>(null)

  const recarregar = useCallback(async () => {
    try {
      setPedidos(await api.get<Pedido[]>('/entregador/pedidos'))
      setErro(null)
    } catch (e) {
      setErro(e instanceof ErroDaApi ? e.message : 'Não foi possível carregar suas entregas.')
    } finally {
      setCarregando(false)
    }
  }, [])

  useEffect(() => {
    void recarregar()
    const id = setInterval(() => void recarregar(), INTERVALO_ATUALIZACAO_MS)

    return () => clearInterval(id)
  }, [recarregar])

  const mover = useCallback(
    async (pedido: Pedido, destino: StatusPedido) => {
      const acao = acaoParaEntregador(pedido, destino)
      if (!acao) return

      const anterior = pedido.status

      setPedidos((atual) => atual.map((p) => (p.id === pedido.id ? { ...p, status: destino } : p)))

      try {
        await api.post(`/entregador/pedidos/${pedido.id}/${acao}`)
        await recarregar()
      } catch (e) {
        setPedidos((atual) => atual.map((p) => (p.id === pedido.id ? { ...p, status: anterior } : p)))
        setErro(e instanceof ErroDaApi ? e.message : 'Não foi possível atualizar a entrega.')
      }
    },
    [recarregar],
  )

  return { pedidos, carregando, erro, mover, recarregar }
}
