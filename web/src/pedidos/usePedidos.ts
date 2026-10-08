import * as signalR from '@microsoft/signalr'
import { useCallback, useEffect, useState } from 'react'
import type { Pedido, StatusPedido } from '@/dominio/pedido'
import { acaoPara } from '@/dominio/pedido'
import { api, ErroDaApi } from '@/lib/api'
import { lerToken } from '@/lib/sessao'

// Fallback de 30s: o push pelo Hub cobre a maioria das atualizações; o poll
// serve só para quando o WebSocket cai (celular em background) ou para a tela
// recém-aberta não esperar o próximo evento.
const INTERVALO_ATUALIZACAO_MS = 30_000

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

  // Push pelo Hub: ingestão de novo pedido (worker iFood) e mutações da API
  // emitem "ResumoAtualizado" no grupo da loja. A tela recarrega sob demanda,
  // sem esperar o fallback de 30s. Falha silenciosa; o poll segue como rede.
  useEffect(() => {
    const conexao = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/rastreio', {
        accessTokenFactory: () => lerToken() ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    conexao.on('ResumoAtualizado', () => {
      void recarregar()
    })

    const iniciar = async () => {
      try {
        await conexao.start()
        await conexao.invoke('EntrarNoGrupo')
      } catch {
        // Fallback do intervalo cobre.
      }
    }

    void iniciar()

    return () => {
      void conexao
        .invoke('SairDoGrupo')
        .catch(() => {})
        .finally(() => conexao.stop())
    }
  }, [recarregar])

  const mover = useCallback(
    async (pedido: Pedido, destino: StatusPedido) => {
      const acao = acaoPara(pedido, destino)
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

  // Cancela em qualquer status não terminal. Motivo é obrigatório (o backend
  // recusa vazio). Devolve a mensagem de erro para exibir junto ao campo, ou
  // null em sucesso — não é otimista de propósito: o dono precisa ver a origem
  // (iFood) aceitar antes do cartão sair do quadro.
  const cancelar = useCallback(
    async (pedido: Pedido, motivo: string): Promise<string | null> => {
      try {
        await api.post(`/pedidos/${pedido.id}/cancelar`, { motivo })
        await recarregar()
        return null
      } catch (e) {
        return e instanceof ErroDaApi ? e.message : 'Não foi possível cancelar o pedido.'
      }
    },
    [recarregar],
  )

  // Pedidos casados: despacha vários numa corrida só com o mesmo motoboy. O
  // sistema calcula a ordem das paradas. Devolve mensagem de erro ou null.
  const despacharLote = useCallback(
    async (entregadorId: string, pedidoIds: string[]): Promise<string | null> => {
      try {
        await api.post('/pedidos/despachar-lote', { entregadorId, pedidoIds })
        await recarregar()
        return null
      } catch (e) {
        return e instanceof ErroDaApi ? e.message : 'Não foi possível despachar o lote.'
      }
    },
    [recarregar],
  )

  return { pedidos, carregando, erro, mover, alocar, cancelar, despacharLote, recarregar }
}
