import { useEffect, useRef, useState } from 'react'
import type { Pedido } from '@/dominio/pedido'
import { api } from '@/lib/api'
import type { PosicaoEntregador } from './useRastreio'

export type EstadoGps = 'inativo' | 'emitindo' | 'negado' | 'indisponivel' | 'erro'

// Mesmo teto do useRastreio: trilha longa demais só consome memória.
const MAX_TRILHA = 200

// Abaixo disso o navegador reenvia praticamente a mesma coordenada e só gera
// escrita à toa no banco.
const INTERVALO_MINIMO_MS = 4_000

function entregaEmCurso(pedidos: Pedido[]): Pedido | undefined {
  return pedidos.find((p) => p.status === 'EmRota' || p.status === 'Chegou')
}

// Emite a posição do motoboy enquanto ele está trabalhando — online, com ou
// sem entrega. É o que permite a loja vê-lo no mapa o tempo todo.
//
// O limite é o turno, não a entrega: offline e sem entrega em curso, nada é
// emitido. O alternador Online/Offline é o consentimento dele.
export function useEnviarPosicao(pedidos: Pedido[], disponivel: boolean | null) {
  const [estado, setEstado] = useState<EstadoGps>('inativo')
  const [posicaoAtual, setPosicaoAtual] = useState<PosicaoEntregador | null>(null)
  const [trilha, setTrilha] = useState<PosicaoEntregador[]>([])
  const ultimoEnvioRef = useRef(0)

  const pedido = entregaEmCurso(pedidos)
  const pedidoId = pedido?.id ?? null
  const deveEmitir = disponivel === true || pedidoId !== null

  // Só o que o balão do mapa precisa. Fora de um effect com o pedido inteiro
  // na dependência, que mudaria de referência a cada polling de 4s.
  const numero = pedido?.numeroExibicao ?? null
  const status = pedido?.status ?? null
  const cliente = pedido?.clienteNome ?? null
  const endereco = pedido?.enderecoResumido ?? null

  useEffect(() => {
    if (!deveEmitir) {
      setEstado('inativo')
      setPosicaoAtual(null)
      setTrilha([])
      return
    }

    // geolocation só existe em contexto seguro: HTTPS ou localhost. No celular
    // acessando por IP da rede local o navegador nem expõe a API.
    if (typeof navigator === 'undefined' || !navigator.geolocation) {
      setEstado('indisponivel')
      return
    }

    const observador = navigator.geolocation.watchPosition(
      (posicao) => {
        setEstado('emitindo')

        // O motoboy não recebe os próprios pings de volta pelo SignalR — eles
        // vão para o grupo da loja. Então o mapa dele é alimentado aqui mesmo.
        const lida: PosicaoEntregador = {
          merchantId: '',
          entregadorId: 'eu',
          entregadorNome: 'Você',
          latitude: posicao.coords.latitude,
          longitude: posicao.coords.longitude,
          precisaoEmMetros: posicao.coords.accuracy,
          capturadoEm: new Date(posicao.timestamp).toISOString(),
          pedidoId,
          pedidoNumero: numero,
          pedidoStatus: status,
          clienteNome: cliente,
          enderecoResumido: endereco,
        }

        setPosicaoAtual(lida)
        if (pedidoId) {
          setTrilha((anterior) => {
            const nova = [...anterior, lida]
            return nova.length > MAX_TRILHA ? nova.slice(nova.length - MAX_TRILHA) : nova
          })
        }

        const agora = Date.now()
        if (agora - ultimoEnvioRef.current < INTERVALO_MINIMO_MS) return
        ultimoEnvioRef.current = agora

        void api
          .post('/entregador/posicao', {
            latitude: posicao.coords.latitude,
            longitude: posicao.coords.longitude,
            precisaoEmMetros: posicao.coords.accuracy,
            capturadoEm: new Date(posicao.timestamp).toISOString(),
          })
          // Ping perdido não é erro que valha interromper a entrega: o próximo
          // chega em segundos e carrega a posição mais recente de qualquer jeito.
          .catch(() => {})
      },
      (erro) => {
        setEstado(erro.code === erro.PERMISSION_DENIED ? 'negado' : 'erro')
      },
      { enableHighAccuracy: true, maximumAge: 0, timeout: 20_000 },
    )

    return () => navigator.geolocation.clearWatch(observador)
  }, [deveEmitir, pedidoId, numero, status, cliente, endereco])

  return { estado, pedidoEmRota: pedido ?? null, posicaoAtual, trilha }
}
