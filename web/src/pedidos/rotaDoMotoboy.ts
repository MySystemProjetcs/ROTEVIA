import type { Pedido } from '@/dominio/pedido'

// Status "ativos" na visão do motoboy: ainda têm que ser visitados. "Concluido"
// e "Cancelado" não entram no sequenciamento — ficam no topo da lista marcados
// como finalizados, para o motoboy ver o que já fez na corrida.
const STATUS_ATIVO = ['Despachado', 'Aceito', 'EmRota', 'Chegou', 'Cobrar'] as const
const STATUS_FINALIZADO = ['Concluido', 'Cancelado'] as const
type StatusFinalizado = (typeof STATUS_FINALIZADO)[number]

/** Posição onde o motoboy está agora (vinda do GPS do app dele). Usada como
 *  origem da sequência quando ele tem múltiplas entregas pendentes. */
export interface PontoDeReferencia {
  latitude: number
  longitude: number
}

/** Resultado por pedido na lista do motoboy. */
export interface ParadaDaCorrida {
  pedido: Pedido
  /** Número da parada (1-based) na sequência total — inclui as já concluídas,
   *  para o indicador na lateral do card mostrar progresso real. */
  ordem: number
  /** Estado da parada no stepper lateral. */
  estado: 'concluida' | 'atual' | 'pendente'
}

// Vizinho-mais-próximo em linha reta (Haversine), igual à heurística que o
// backend usa no DespacharEmLote. É a mesma Opção A: ignora ruas/mão única,
// mas resolve o pedido do motoboy quando ele tem múltiplas entregas na mão
// — a mais próxima vira a primeira a sair, a mais distante vai pro fim.
//
// Sem coordenada = vai pro fim na ordem de entrada (não há como sequenciar).
// 0,0 é tratado como "sem coordenada" (proteção contra o sandbox do iFood).
export function sequenciar(pedidos: Pedido[], origem: PontoDeReferencia): Pedido[] {
  const comCoord: Array<{ pedido: Pedido; lat: number; lng: number }> = []
  const semCoord: Pedido[] = []

  for (const pedido of pedidos) {
    const lat = pedido.enderecoLatitude
    const lng = pedido.enderecoLongitude
    if (lat != null && lng != null && (lat !== 0 || lng !== 0)) {
      comCoord.push({ pedido, lat, lng })
    } else {
      semCoord.push(pedido)
    }
  }

  const ordenados: Pedido[] = []
  let atualLat = origem.latitude
  let atualLng = origem.longitude

  while (comCoord.length > 0) {
    let idxMaisProximo = 0
    let menorDistancia = distanciaKm(atualLat, atualLng, comCoord[0].lat, comCoord[0].lng)
    for (let i = 1; i < comCoord.length; i++) {
      const d = distanciaKm(atualLat, atualLng, comCoord[i].lat, comCoord[i].lng)
      if (d < menorDistancia) {
        menorDistancia = d
        idxMaisProximo = i
      }
    }
    const [proxima] = comCoord.splice(idxMaisProximo, 1)
    ordenados.push(proxima.pedido)
    atualLat = proxima.lat
    atualLng = proxima.lng
  }

  return [...ordenados, ...semCoord]
}

/**
 * Monta a sequência de paradas de um motoboy com múltiplos pedidos:
 *   1. Concluídos/cancelados primeiro, em ordem de "quando foram feitos"
 *      (recebidoEm) — são as paradas já cumpridas, aparecem no topo com o
 *      indicador "concluída".
 *   2. Em seguida os pendentes, sequenciados por proximidade a partir do
 *      ponto de referência (GPS do motoboy, ou da loja na ausência de GPS).
 *      O primeiro pendente vira a parada "atual"; os demais ficam "pendentes".
 *
 * Prioridade do sequenciamento (ordem de preferência):
 *   - Se os pedidos pendentes vieram todos de um mesmo lote (LoteEntregaId),
 *     respeita a OrdemNaRota já calculada pelo backend no despacho em lote
 *     (mais confiável, considera a origem comum da loja).
 *   - Senão, sequencia no cliente por vizinho-mais-próximo.
 */
export function montarCorrida(
  pedidos: Pedido[],
  referencia: PontoDeReferencia | null,
): ParadaDaCorrida[] {
  const finalizados = pedidos
    .filter((p) => (STATUS_FINALIZADO as readonly string[]).includes(p.status))
    .sort((a, b) => new Date(a.recebidoEm).getTime() - new Date(b.recebidoEm).getTime())

  const pendentes = pedidos.filter((p) =>
    (STATUS_ATIVO as readonly string[]).includes(p.status),
  )

  const pendentesOrdenados = ordenarPendentes(pendentes, referencia)

  const paradas: ParadaDaCorrida[] = []
  let ordem = 1

  for (const pedido of finalizados) {
    paradas.push({ pedido, ordem, estado: 'concluida' })
    ordem++
  }

  pendentesOrdenados.forEach((pedido, i) => {
    paradas.push({
      pedido,
      ordem,
      estado: i === 0 ? 'atual' : 'pendente',
    })
    ordem++
  })

  return paradas
}

function ordenarPendentes(
  pendentes: Pedido[],
  referencia: PontoDeReferencia | null,
): Pedido[] {
  if (pendentes.length < 2) return pendentes

  // Caminho confiável: todos no mesmo lote → o backend já sequenciou
  // considerando a loja de origem. Só respeita OrdemNaRota.
  const idsDeLote = new Set(
    pendentes.map((p) => p.loteEntregaId).filter((id): id is string => !!id),
  )
  const todosDoMesmoLote =
    idsDeLote.size === 1
    && pendentes.every((p) => p.loteEntregaId !== null && p.ordemNaRota !== null)

  if (todosDoMesmoLote) {
    return [...pendentes].sort(
      (a, b) => (a.ordemNaRota as number) - (b.ordemNaRota as number),
    )
  }

  // Sem referência de origem (GPS indisponível) e sem lote: devolve na ordem de
  // chegada — melhor que inventar uma sequência errada a partir de ponto fixo
  // que pode estar longe de qualquer entrega.
  if (!referencia) return pendentes

  return sequenciar(pendentes, referencia)
}

function distanciaKm(lat1: number, lng1: number, lat2: number, lng2: number): number {
  const R = 6371
  const dLat = ((lat2 - lat1) * Math.PI) / 180
  const dLng = ((lng2 - lng1) * Math.PI) / 180
  const a =
    Math.sin(dLat / 2) ** 2
    + Math.cos((lat1 * Math.PI) / 180) * Math.cos((lat2 * Math.PI) / 180) * Math.sin(dLng / 2) ** 2
  return R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a))
}

export type { StatusFinalizado }
