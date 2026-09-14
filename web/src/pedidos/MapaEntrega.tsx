// maplibre-gl 6 é ESM puro e não tem export default — só nomeados. "Map" vem
// aliasado como MapLibreMap para não colidir com o Map do JavaScript.
import {
  AttributionControl,
  MapLibreMap,
  Marker,
  NavigationControl,
  Popup,
  type GeoJSONSource,
} from 'maplibre-gl'
import 'maplibre-gl/dist/maplibre-gl.css'
import { useEffect, useRef } from 'react'
import iconeLojaUrl from '@/assets/icone-loja.svg'
import iconeMotoboyUrl from '@/assets/icone-motoboy.svg'
import type { PosicaoEntregador } from './useRastreio'

// OpenFreeMap: vetorial, sem chave de API e sem limite de requisição, uso
// comercial liberado. É a escolha que respeita o §3 do CLAUDE.md — Google e
// Mapbox só na navegação final do motoboy, porque custo de API destrói margem
// em SMB.
//
// "liberty" e não "fiord": conferido no style.json, só o liberty traz camadas
// fill-extrusion, que são o que levanta os prédios em 3D.
const ESTILO_3D = 'https://tiles.openfreemap.org/styles/liberty'

// Exigência de atribuição do OpenFreeMap.
const ATRIBUICAO = 'OpenFreeMap © OpenMapTiles Data from OpenStreetMap'

const ORIGEM_TRILHA = 'trilha-motoboy'
const CAMADA_TRILHA = 'trilha-motoboy-linha'

// Cor vinda do tema, não do componente: o canvas do MapLibre não aceita classe
// do Tailwind, então lemos o mesmo token que o resto da interface usa.
function corDoTema(token: string, alternativa: string): string {
  if (typeof window === 'undefined') return alternativa
  const valor = getComputedStyle(document.documentElement).getPropertyValue(token).trim()
  return valor || alternativa
}

// Marcador como elemento do DOM: assim a aparência sai de classe do Tailwind
// em vez de estilo embutido.
function criarMarcador(classes: string): HTMLElement {
  const elemento = document.createElement('div')
  elemento.className = classes
  return elemento
}

// O marcador do MapLibre é DOM puro, fora da árvore do React, então a imagem
// entra por elemento criado na mão em vez de JSX. Como ilustração colorida, o
// SVG vive em assets — as cores ficam no arquivo, não no componente.
function criarMarcadorComImagem(classes: string, src: string, descricao: string): HTMLElement {
  const elemento = criarMarcador(classes)
  const imagem = document.createElement('img')
  imagem.src = src
  imagem.alt = descricao
  imagem.className = 'size-full'
  elemento.appendChild(imagem)
  return elemento
}

interface Coordenada {
  latitude: number
  longitude: number
}

// Texto do balão que aparece ao passar o mouse no motoboy. Sem pedido, só o
// nome — é o motoboy online esperando entrega.
function detalheDoMotoboy(p: PosicaoEntregador): string {
  if (!p.pedidoId) return `${p.entregadorNome}\nOnline, sem entrega`

  return [
    p.entregadorNome,
    `Pedido #${p.pedidoNumero}`,
    p.pedidoStatus === 'Chegou' ? 'No local' : 'A caminho',
    p.clienteNome,
    p.enderecoResumido,
  ]
    .filter(Boolean)
    .join('\n')
}

interface MapaEntregaProps {
  /** Uma posição por motoboy online. */
  posicoes: PosicaoEntregador[]
  trilha: PosicaoEntregador[]
  enderecoEntrega?: Coordenada | null
  /** Ponto fixo da loja — é de onde a entrega sai. */
  loja?: Coordenada | null
  className?: string
}

export function MapaEntrega({
  posicoes,
  trilha,
  enderecoEntrega,
  loja,
  className,
}: MapaEntregaProps) {
  const divRef = useRef<HTMLDivElement>(null)
  const mapaRef = useRef<MapLibreMap | null>(null)
  const prontoRef = useRef(false)
  // Um marcador por motoboy, indexado pelo id dele.
  const motoboysRef = useRef<Map<string, Marker>>(new Map())
  const destinoRef = useRef<Marker | null>(null)
  const lojaRef = useRef<Marker | null>(null)
  const seguiuRef = useRef(false)

  useEffect(() => {
    if (!divRef.current || mapaRef.current) return

    // Capturado aqui porque a limpeza abaixo o usa: a instância do Map nunca
    // troca, mas ler .current dentro do cleanup dispara aviso do linter.
    const marcadoresDeMotoboy = motoboysRef.current

    const mapa = new MapLibreMap({
      container: divRef.current,
      style: ESTILO_3D,
      center: [-46.6092766, -23.6282109],
      zoom: 14,
      // Câmera inclinada: é o que faz a extrusão dos prédios aparecer.
      pitch: 50,
      bearing: -20,
      attributionControl: false,
    })

    mapa.addControl(new AttributionControl({ customAttribution: ATRIBUICAO }), 'bottom-right')
    mapa.addControl(new NavigationControl({ visualizePitch: true }), 'top-right')

    mapa.on('load', () => {
      mapa.addSource(ORIGEM_TRILHA, {
        type: 'geojson',
        data: { type: 'Feature', properties: {}, geometry: { type: 'LineString', coordinates: [] } },
      })

      mapa.addLayer({
        id: CAMADA_TRILHA,
        type: 'line',
        source: ORIGEM_TRILHA,
        layout: { 'line-cap': 'round', 'line-join': 'round' },
        paint: {
          'line-color': corDoTema('--color-marcador-motoboy', '#4f46e5'),
          'line-width': 4,
          'line-opacity': 0.85,
        },
      })

      prontoRef.current = true
    })

    mapaRef.current = mapa

    return () => {
      prontoRef.current = false
      mapa.remove()
      mapaRef.current = null

      // Zerar as refs dos marcadores junto com o mapa é obrigatório. O
      // StrictMode monta, desmonta e remonta: se elas sobrevivessem, os
      // efeitos abaixo veriam um marcador "já existente" — preso ao mapa
      // destruído — e só tentariam reposicioná-lo, sem nunca adicionar
      // marcador nenhum ao mapa novo.
      marcadoresDeMotoboy.clear()
      destinoRef.current = null
      lojaRef.current = null
      seguiuRef.current = false
    }
  }, [])

  // Pin fixo da loja.
  useEffect(() => {
    const mapa = mapaRef.current
    if (!mapa || !loja) return

    const ponto: [number, number] = [loja.longitude, loja.latitude]

    if (lojaRef.current) {
      lojaRef.current.setLngLat(ponto)
      return
    }

    lojaRef.current = new Marker({
      element: criarMarcadorComImagem('size-9 drop-shadow-md', iconeLojaUrl, 'Loja'),
    })
      .setLngLat(ponto)
      .setPopup(new Popup({ offset: 12 }).setText('Loja'))
      .addTo(mapa)
  }, [loja])

  // Pin do endereço de entrega.
  useEffect(() => {
    const mapa = mapaRef.current
    if (!mapa || !enderecoEntrega) return

    const ponto: [number, number] = [enderecoEntrega.longitude, enderecoEntrega.latitude]

    if (destinoRef.current) {
      destinoRef.current.setLngLat(ponto)
      return
    }

    destinoRef.current = new Marker({
      element: criarMarcador('size-4 rounded-full border-2 border-superficie bg-estado-chegou shadow-elevado'),
    })
      .setLngLat(ponto)
      .setPopup(new Popup({ offset: 12 }).setText('Entrega'))
      .addTo(mapa)
  }, [enderecoEntrega])

  // Um marcador por motoboy + trilha da entrega em curso.
  useEffect(() => {
    const mapa = mapaRef.current
    if (!mapa || posicoes.length === 0) return

    const vistos = new Set<string>()

    for (const posicao of posicoes) {
      vistos.add(posicao.entregadorId)
      const ponto: [number, number] = [posicao.longitude, posicao.latitude]
      const existente = motoboysRef.current.get(posicao.entregadorId)

      if (existente) {
        existente.setLngLat(ponto)
        // O balão é recriado a cada ping porque o status do pedido muda no
        // meio da entrega — "a caminho" vira "no local".
        existente.getPopup()?.setText(detalheDoMotoboy(posicao))
        continue
      }

      const elemento = criarMarcadorComImagem('size-9 drop-shadow-md', iconeMotoboyUrl, posicao.entregadorNome)
      const balao = new Popup({ offset: 16, closeButton: false }).setText(detalheDoMotoboy(posicao))
      const marcador = new Marker({ element: elemento }).setLngLat(ponto).setPopup(balao).addTo(mapa)

      // Abre ao passar o mouse, não só no clique: o dono quer conferir de
      // relance quem é e para onde vai, sem perder o que estava fazendo.
      elemento.addEventListener('mouseenter', () => marcador.togglePopup())
      elemento.addEventListener('mouseleave', () => marcador.togglePopup())

      motoboysRef.current.set(posicao.entregadorId, marcador)
    }

    // Quem ficou offline some do mapa.
    for (const [id, marcador] of motoboysRef.current) {
      if (vistos.has(id)) continue
      marcador.remove()
      motoboysRef.current.delete(id)
    }

    // Centraliza só no primeiro ping: depois disso mexer a câmera sozinho
    // atrapalharia quem está arrastando o mapa para olhar outra coisa.
    if (!seguiuRef.current) {
      const primeira = posicoes[0]
      mapa.easeTo({ center: [primeira.longitude, primeira.latitude], zoom: 15, duration: 800 })
      seguiuRef.current = true
    }

    if (!prontoRef.current) return

    const origem = mapa.getSource(ORIGEM_TRILHA) as GeoJSONSource | undefined
    origem?.setData({
      type: 'Feature',
      properties: {},
      geometry: {
        type: 'LineString',
        coordinates: trilha.map((p) => [p.longitude, p.latitude]),
      },
    })
  }, [posicoes, trilha])

  return <div ref={divRef} className={className} />
}
