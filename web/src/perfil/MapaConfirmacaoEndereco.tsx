import MaplibreGeocoder from '@maplibre/maplibre-gl-geocoder'
import '@maplibre/maplibre-gl-geocoder/dist/maplibre-gl-geocoder.css'
import type { MaplibreGeocoderApi, MaplibreGeocoderFeatureResults } from '@maplibre/maplibre-gl-geocoder'
import * as maplibregl from 'maplibre-gl'
import { Marker, NavigationControl, MapLibreMap as MapaLibre } from 'maplibre-gl'
import 'maplibre-gl/dist/maplibre-gl.css'
import '@/lib/maplibreWorker'
import type { Feature, Point } from 'geojson'
import { useEffect, useRef } from 'react'

const ESTILO = 'https://tiles.openfreemap.org/styles/liberty'

// Centro do Brasil — só usado quando ainda não há nenhuma coordenada (CEP
// novo, sem busca ainda feita). Assim que o CEP resolve algo, o mapa recentra
// na posição real; este ponto nunca aparece como pin em si.
const CENTRO_BRASIL: [number, number] = [-51.9253, -14.235]

// Busca de endereço pelo Nominatim (OpenStreetMap), restrita ao Brasil — a
// mesma fonte gratuita que o backend já usa para geocodificar CEP. Formato de
// retorno documentado em @maplibre/maplibre-gl-geocoder: cada feature precisa
// de place_name, geometry (Point) e center.
const geocoderApi: MaplibreGeocoderApi = {
  forwardGeocode: async (config): Promise<MaplibreGeocoderFeatureResults> => {
    const consulta = typeof config.query === 'string' ? config.query : ''
    if (!consulta.trim()) return { type: 'FeatureCollection', features: [] }

    try {
      const resposta = await fetch(
        `https://nominatim.openstreetmap.org/search?format=geojson&addressdetails=1&limit=5&countrycodes=br&q=${encodeURIComponent(consulta)}`,
      )
      const geojson = await resposta.json()

      const features = (geojson.features ?? []).map((feature: Feature) => {
        const bbox = (feature as { bbox?: [number, number, number, number] }).bbox
        const centro: [number, number] =
          feature.geometry.type === 'Point'
            ? (feature.geometry.coordinates as [number, number])
            : bbox
              ? [(bbox[0] + bbox[2]) / 2, (bbox[1] + bbox[3]) / 2]
              : [0, 0]

        const nome = (feature.properties as { display_name?: string })?.display_name ?? consulta

        return {
          type: 'Feature' as const,
          geometry: { type: 'Point' as const, coordinates: centro },
          place_name: nome,
          properties: feature.properties ?? {},
          text: nome,
          place_type: ['place'],
          center: centro,
        }
      })

      return { type: 'FeatureCollection', features }
    } catch {
      // Busca falhou (rede, Nominatim fora do ar): devolve vazio — o campo de
      // texto continua utilizável, só não sugere nada desta vez.
      return { type: 'FeatureCollection', features: [] }
    }
  },
}

interface MapaConfirmacaoEnderecoProps {
  latitude: number | null
  longitude: number | null
  onMudarPosicao: (latitude: number, longitude: number) => void
}

// Confirmação visual da coordenada antes de salvar: o pino nasce onde o
// backend geocodificou (CEP/Nominatim/Google, o que tiver resolvido) e pode
// ser arrastado até a porta certa, ou reposicionado buscando um lugar pelo
// nome. É a salvaguarda que funciona mesmo quando NENHUMA fonte de geocodi-
// ficação tem o prédio exato — quem confirma por último é a pessoa olhando o
// mapa, não a precisão de um serviço de terceiro.
export function MapaConfirmacaoEndereco({
  latitude,
  longitude,
  onMudarPosicao,
}: MapaConfirmacaoEnderecoProps) {
  const divRef = useRef<HTMLDivElement>(null)
  const mapaRef = useRef<MapaLibre | null>(null)
  const marcadorRef = useRef<Marker | null>(null)
  // Última posição aplicada pelas props — distingue "o pai mandou uma
  // coordenada nova" (recentra o mapa) de "o próprio arrasto/busca mudou a
  // posição" (não recentra, senão o mapa pula de volta embaixo do dedo de
  // quem está arrastando).
  const ultimaPropRef = useRef<string | null>(null)
  // Prop mais recente de onMudarPosicao, lida pelos handlers do MapLibre —
  // eles são registrados uma vez na montagem e não podem fechar sobre uma
  // versão velha do callback.
  const onMudarPosicaoRef = useRef(onMudarPosicao)
  onMudarPosicaoRef.current = onMudarPosicao

  useEffect(() => {
    if (!divRef.current) return

    const pontoInicial: [number, number] =
      latitude !== null && longitude !== null ? [longitude, latitude] : CENTRO_BRASIL

    const mapa = new MapaLibre({
      container: divRef.current,
      style: ESTILO,
      center: pontoInicial,
      zoom: latitude !== null ? 17 : 4,
      attributionControl: false,
    })

    mapa.addControl(new NavigationControl({ showCompass: false }), 'top-right')

    const geocoder = new MaplibreGeocoder(geocoderApi, {
      maplibregl: maplibregl as unknown as typeof import('maplibre-gl'),
      marker: false,
      // Já existe um pino nosso, arrastável — o próprio marker interno do
      // geocoder duplicaria o ponto.
      placeholder: 'Buscar endereço ou ponto de referência…',
      collapsed: false,
    })
    mapa.addControl(geocoder, 'top-left')

    geocoder.on('result', (evento) => {
      const centro = evento.result.center ?? (evento.result.geometry as Point).coordinates
      const [lng, lat] = centro as [number, number]
      marcadorRef.current?.setLngLat([lng, lat])
      mapa.flyTo({ center: [lng, lat], zoom: 17, duration: 800 })
      onMudarPosicaoRef.current(lat, lng)
    })

    const marcador = new Marker({ draggable: true, color: '#4f46e5' })
      .setLngLat(pontoInicial)
      .addTo(mapa)

    marcador.on('dragend', () => {
      const { lat, lng } = marcador.getLngLat()
      onMudarPosicaoRef.current(lat, lng)
    })

    mapaRef.current = mapa
    marcadorRef.current = marcador
    ultimaPropRef.current = `${latitude},${longitude}`

    return () => {
      geocoder.onRemove()
      marcador.remove()
      mapa.remove()
      mapaRef.current = null
      marcadorRef.current = null
    }
    // Intencional: o mapa monta uma vez. Mudança de latitude/longitude depois
    // da montagem é tratada pelo efeito abaixo (recentra sem recriar o mapa),
    // porque recriar a cada dígito do CEP destruiria o estado do usuário
    // arrastando o pino.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // Recentra quando o pai manda uma coordenada nova (ex.: CEP novo resolvido)
  // — mas não quando a mudança veio do próprio arrasto/busca daqui dentro,
  // senão o pino "pula de volta" sob o dedo de quem está arrastando.
  useEffect(() => {
    const chave = `${latitude},${longitude}`
    if (chave === ultimaPropRef.current) return
    ultimaPropRef.current = chave

    const mapa = mapaRef.current
    const marcador = marcadorRef.current
    if (!mapa || !marcador || latitude === null || longitude === null) return

    marcador.setLngLat([longitude, latitude])
    mapa.flyTo({ center: [longitude, latitude], zoom: 17, duration: 800 })
  }, [latitude, longitude])

  return (
    <div
      ref={divRef}
      className="h-64 w-full overflow-hidden rounded-controle border border-borda-forte"
    />
  )
}
