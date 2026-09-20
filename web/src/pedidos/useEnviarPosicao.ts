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

// Leitura pior que isto não é posição, é palpite: a 96 m de incerteza — valor
// real visto nos dados — o ponto cai a um quarteirão de distância e põe o
// motoboy na rua errada. Melhor manter a última posição boa do que desenhar
// uma mentira precisa.
const PRECISAO_MAXIMA_M = 50

// Distância em metros entre duas coordenadas (Haversine). Aproximação de
// esfera basta: a mil metros o erro é de centímetros.
function distanciaEmMetros(
  aLat: number,
  aLon: number,
  bLat: number,
  bLon: number,
): number {
  const R = 6_371_000
  const rad = Math.PI / 180
  const dLat = (bLat - aLat) * rad
  const dLon = (bLon - aLon) * rad
  const a =
    Math.sin(dLat / 2) ** 2 +
    Math.cos(aLat * rad) * Math.cos(bLat * rad) * Math.sin(dLon / 2) ** 2

  return 2 * R * Math.asin(Math.sqrt(a))
}

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
  // Última leitura aceita, para medir se houve deslocamento de verdade.
  const ultimaBoaRef = useRef<{ lat: number; lon: number } | null>(null)

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

        const { latitude, longitude, accuracy } = posicao.coords

        // Descarta a leitura ruim em vez de teleportar o marcador.
        if (accuracy > PRECISAO_MAXIMA_M) return

        // Parado, o GPS treme dentro do próprio raio de erro e o marcador fica
        // dançando na calçada. Só move quando o deslocamento supera a
        // incerteza da leitura — aí é caminhada, não ruído.
        const anterior = ultimaBoaRef.current
        if (
          anterior &&
          distanciaEmMetros(anterior.lat, anterior.lon, latitude, longitude) < accuracy
        ) {
          return
        }

        ultimaBoaRef.current = { lat: latitude, lon: longitude }

        // O motoboy não recebe os próprios pings de volta pelo SignalR — eles
        // vão para o grupo da loja. Então o mapa dele é alimentado aqui mesmo.
        const lida: PosicaoEntregador = {
          merchantId: '',
          entregadorId: 'eu',
          entregadorNome: 'Você',
          latitude,
          longitude,
          precisaoEmMetros: accuracy,
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
            latitude,
            longitude,
            precisaoEmMetros: accuracy,
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
