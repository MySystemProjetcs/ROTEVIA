import { useCallback, useEffect, useState } from 'react'
import type { Pedido, StatusPedido } from '@/dominio/pedido'
import { acaoPara } from '@/dominio/pedido'
import { api, ErroDaApi } from '@/lib/api'

const INTERVALO_ATUALIZACAO_MS = 4_000

export function usePedidos() {
  const [pedidos, setPedidos] = useState<Pedido[]>([])
  const [carregando, setCarregando] = useState(true)
  const [erro, setErro] = useState<string | null>(null)

  const recarregar = useCallback(async () => {
    try {
      setPedidos(await api.get<Pedido[]>('/pedidos'))
      setErro(null)
    } catch (e) {
      setErro(e instanceof ErroDaApi ? e.message : 'Não foi possível carregar os pedidos.')
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
      const acao = acaoPara(pedido.status, destino)
      if (!acao) return

      const anterior = pedido.status

      // Otimista: a cozinha não pode esperar a ida e volta da rede para ver o
      // cartão sair da coluna. Se a API recusar, o estado volta e o erro
      // aparece — travar a interface a cada clique seria pior.
      setPedidos((atual) => atual.map((p) => (p.id === pedido.id ? { ...p, status: destino } : p)))

      try {
        await api.post(`/pedidos/${pedido.id}/${acao}`)
        await recarregar()
      } catch (e) {
        setPedidos((atual) => atual.map((p) => (p.id === pedido.id ? { ...p, status: anterior } : p)))
        setErro(e instanceof ErroDaApi ? e.message : 'Não foi possível atualizar o pedido.')
      }
    },
    [recarregar],
  )

  const alocar = useCallback(
    async (pedido: Pedido, entregadorId: string) => {
      try {
        await api.post(`/pedidos/${pedido.id}/alocar-entregador`, { entregadorId })
        await recarregar()
      } catch (e) {
        setErro(e instanceof ErroDaApi ? e.message : 'Não foi possível alocar o motoboy.')
      }
    },
    [recarregar],
  )

  return { pedidos, carregando, erro, mover, alocar, recarregar }
}
