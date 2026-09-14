import * as signalR from '@microsoft/signalr'
import { useEffect, useRef, useState } from 'react'
import { api } from '@/lib/api'
import { lerToken } from '@/lib/sessao'

export interface PosicaoEntregador {
  merchantId: string
  entregadorId: string
  entregadorNome: string
  latitude: number
  longitude: number
  precisaoEmMetros: number
  capturadoEm: string
  // Nulos quando o motoboy está online sem entrega — é assim que a loja o vê
  // parado no mapa, esperando pedido.
  pedidoId: string | null
  pedidoNumero: string | null
  pedidoStatus: string | null
  clienteNome: string | null
  enderecoResumido: string | null
}

// Máximo de pings mantidos em memória para desenhar a trilha no mapa.
// Além desse limite os mais antigos são descartados — evita crescimento
// ilimitado numa entrega muito longa.
const MAX_TRILHA = 200

// Conecta ao RastreioHub e escuta posições do motoboy para um pedido específico.
// O restaurante entra no grupo do seu merchantId — o servidor filtra pelo
// pedido antes de emitir, então recebe só pings relevantes.
export function useRastreio(merchantId: string | null | undefined) {
  // Um por motoboy: a loja pode ter vários online ao mesmo tempo, e cada um
  // tem seu marcador no mapa.
  const [porEntregador, setPorEntregador] = useState<Record<string, PosicaoEntregador>>({})
  const [trilha, setTrilha] = useState<PosicaoEntregador[]>([])
  const [conectado, setConectado] = useState(false)
  const conexaoRef = useRef<signalR.HubConnection | null>(null)

  // Estado inicial vindo do cache do servidor. O SignalR não reenvia o que já
  // passou: sem isto, depois de um F5 o mapa fica vazio até algum motoboy
  // emitir o próximo ping — que pode demorar se a aba dele estiver em segundo
  // plano, quando o navegador estrangula os temporizadores.
  useEffect(() => {
    if (!merchantId) return

    let cancelado = false

    api
      .get<PosicaoEntregador[]>('/rastreio/posicoes')
      .then((iniciais) => {
        if (cancelado) return
        setPorEntregador((anterior) => {
          const mapa = { ...anterior }
          // Ping que chegou pelo push enquanto isto carregava é mais novo:
          // não pode ser sobrescrito pelo cache.
          for (const p of iniciais) mapa[p.entregadorId] ??= p
          return mapa
        })
      })
      .catch(() => {})

    return () => {
      cancelado = true
    }
  }, [merchantId])

  useEffect(() => {
    if (!merchantId) return

    const conexao = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/rastreio', {
        // Bearer via query string: padrão do SignalR JS para WebSocket
        // (CLAUDE.md §3 — autenticação por Bearer token, nunca cookie).
        accessTokenFactory: () => lerToken() ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    conexaoRef.current = conexao

    conexao.on('PosicaoAtualizada', (posicao: PosicaoEntregador) => {
      setPorEntregador((anterior) => ({ ...anterior, [posicao.entregadorId]: posicao }))

      // A trilha desenhada é só a da entrega em curso: rastro de motoboy
      // parado esperando pedido não diz nada e ainda suja o mapa.
      if (!posicao.pedidoId) return

      setTrilha((anterior) => {
        const nova = [...anterior, posicao]
        return nova.length > MAX_TRILHA ? nova.slice(nova.length - MAX_TRILHA) : nova
      })
    })

    // Quem atualiza o resumo é o useResumoDashboard, na conexão dele. Esta
    // ignora de propósito: as duas entram no mesmo grupo, então a mensagem
    // chega aqui também, e sem este registro o SignalR enche o console de
    // "No client method with the name 'resumoatualizado' found".
    conexao.on('ResumoAtualizado', () => {})

    conexao.onreconnecting(() => setConectado(false))
    conexao.onreconnected(() => setConectado(true))
    conexao.onclose(() => setConectado(false))

    const iniciar = async () => {
      try {
        await conexao.start()
        // Sem argumento: o servidor resolve a loja pelo token. Mandar o id
        // daqui não teria valor — ele não é confiável do outro lado.
        await conexao.invoke('EntrarNoGrupo')
        setConectado(true)
      } catch {
        // Silencioso: withAutomaticReconnect cuida da retentativa.
        setConectado(false)
      }
    }

    void iniciar()

    return () => {
      void conexao
        .invoke('SairDoGrupo')
        .catch(() => {})
        .finally(() => conexao.stop())
    }
  }, [merchantId])

  // Limpa a trilha quando troca de pedido acompanhado.
  const limparTrilha = () => setTrilha([])

  const posicoes = Object.values(porEntregador)

  return { posicoes, trilha, conectado, limparTrilha }
}
