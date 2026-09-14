import { useEffect, useState } from 'react'
import { useSessao } from '@/auth/SessaoProvider'
import type { Pedido } from '@/dominio/pedido'
import { api } from '@/lib/api'

// Mais folgado que o poll do painel (4s): o contador é aviso periférico, não
// precisa da mesma resolução do quadro que a pessoa está olhando.
const INTERVALO_MS = 8_000

// Quantos pedidos estão esperando ação de quem está logado. Para o dono é o
// que ainda não foi confirmado; para o motoboy, o que foi despachado e aguarda
// o aceite dele.
export function usePedidosAguardando(): number {
  const { usuario } = useSessao()
  const ehEntregador = usuario?.papel === 'Entregador'
  const [quantidade, setQuantidade] = useState(0)

  useEffect(() => {
    if (!usuario) return

    const caminho = ehEntregador ? '/entregador/pedidos' : '/pedidos'
    const aguardando = ehEntregador ? 'Despachado' : 'Recebido'
    let cancelado = false

    const buscar = async () => {
      try {
        const pedidos = await api.get<Pedido[]>(caminho)
        if (!cancelado) setQuantidade(pedidos.filter((p) => p.status === aguardando).length)
      } catch {
        // Silencioso: contador é informação secundária, e o painel ao lado já
        // mostra o erro de carregamento se a API estiver fora.
      }
    }

    void buscar()
    const id = setInterval(() => void buscar(), INTERVALO_MS)

    return () => {
      cancelado = true
      clearInterval(id)
    }
  }, [usuario, ehEntregador])

  return quantidade
}
