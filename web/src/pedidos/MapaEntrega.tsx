// maplibre-gl 6 é ESM puro e não tem export default — só nomeados. "Map" vem
// aliasado como MapLibreMap para não colidir com o Map do JavaScript.
import {
  AttributionControl,
  MapLibreMap,
  Marker,
  NavigationControl,
  Popup,
  setWorkerUrl,
} from 'maplibre-gl'
import 'maplibre-gl/dist/maplibre-gl.css'
// O MapLibre monta o worker com `new URL('./maplibre-gl-worker.mjs',
// import.meta.url)`. Em dev isso cai no arquivo real dentro de node_modules; no
// build, `import.meta.url` é o chunk em /assets/, e o worker vira um 404. O
// mapa sobe, os marcadores aparecem e nenhum tile renderiza — porque quem
// decodifica tile é o worker.
//
// `?worker&url` faz o Vite EMPACOTAR o worker — com o maplibre-gl-shared que
// ele importa — e devolver a URL do arquivo gerado. Com `?url` puro o arquivo
// seria copiado cru e o import interno dele viraria outro 404.
import workerUrl from 'maplibre-gl/dist/maplibre-gl-worker.mjs?worker&url'
import { useEffect, useRef } from 'react'
import pinoLojaUrl from '@/assets/pino-loja.svg'
import pinoMotoboyUrl from '@/assets/pino-motoboy.svg'
import type { Pedido, StatusPedido } from '@/dominio/pedido'
import { TITULO_COLUNA } from '@/dominio/pedido'
import { formatarDinheiro } from '@/lib/tempo'
import { CROMO_DA_COLUNA } from './cromoDoStatus'
import type { PosicaoEntregador } from './useRastreio'

setWorkerUrl(workerUrl)

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

// Marcador como elemento do DOM: assim a aparência sai de classe do Tailwind
// em vez de estilo embutido.
function criarMarcador(classes: string): HTMLElement {
  const elemento = document.createElement('div')
  elemento.className = classes
  return elemento
}

// O marcador do MapLibre é DOM puro, fora da árvore do React, então a imagem
// entra por elemento criado na mão em vez de JSX. O pino inteiro — corpo em
// gota, brilho, disco e sombra de contato — vive no arquivo SVG: as cores e o
// desenho ficam no asset, o componente só decide tamanho.
function criarPino(src: string, descricao: string): HTMLElement {
  const imagem = document.createElement('img')
  imagem.src = src
  imagem.alt = descricao
  // Só largura: a altura sai da proporção 64x84 do desenho. `size-*` forçaria
  // um quadrado e achataria o pino.
  imagem.className = 'w-10 max-w-none'
  return imagem
}

// Balão com o número do pedido, apontando para a coordenada exata da entrega.
// Cor vinda do mesmo cromo das colunas do Kanban, então um pedido "Em rota" é
// da mesma cor nos dois lugares.
//
// O corpo usa o `pill` (fundo claro, texto na cor forte) e não a cor cheia com
// texto branco: sobre o âmbar do Recebido o branco fica em ~2:1 de contraste, e
// o número — que é a razão de o balão existir — vira borrão sobre o mapa.
function criarBalaoDePedido(numero: string, cromo: { ponto: string; pill: string }): HTMLElement {
  const elemento = criarMarcador('flex flex-col items-center drop-shadow-md')

  const etiqueta = document.createElement('span')
  etiqueta.className = `rounded-controle border px-1.5 py-0.5 text-rotulo font-bold tabular-nums ${cromo.pill}`
  etiqueta.textContent = `#${numero}`

  // Quadrado girado 45°: vira a ponta do balão sem precisar de SVG. Na cor
  // cheia, que é o que marca o ponto no mapa a distância.
  const ponta = document.createElement('span')
  ponta.className = `-mt-1 size-2 rotate-45 ${cromo.ponto}`

  elemento.append(etiqueta, ponta)
  return elemento
}

// O desenho reserva 7 de 84 unidades abaixo da ponta para a sombra de contato.
// Sem descer o marcador, quem encostaria na coordenada seria a sombra, não a
// ponta — a uns 4px de erro na largura em que o pino é exibido.
const DESLOCAMENTO_DA_PONTA: [number, number] = [0, 4]

// O deslize dura o intervalo real entre pings, medido em tempo de execução: é
// o que mantém o marcador em movimento contínuo. Animar mais rápido que a
// cadência devolveria o picotado — anda, para, anda, para.
//
// O preço é o marcador ficar até um intervalo atrás da posição real. É a mesma
// troca que Uber e iFood fazem: movimento crível vale mais que precisão
// instantânea num ponto que o GPS do celular já entrega com erro de metros.
const DESLIZE_MINIMO_MS = 600
const DESLIZE_MAXIMO_MS = 6_000
const DESLIZE_PADRAO_MS = 4_000

// Linear de propósito, sem aceleração nem frenagem. Cada ping é um ponto de uma
// rota contínua, não um destino: com easing o marcador desacelera até parar em
// cada ponto e arranca no seguinte — que é o picotado que se queria remover.
// Linear, com a duração casada à cadência, o motoboy anda sem parar.

// Desliza o marcador até a posição nova em vez de teleportá-lo. Sem isto o
// motoboy "pula" a cada ping do GPS, que é o travamento que se vê na tela —
// não é lentidão da rede, é ausência de interpolação.
function deslizarMarcador(
  marcador: Marker,
  destino: [number, number],
  animacoes: Map<string, number>,
  ultimoPing: Map<string, number>,
  chave: string,
): void {
  const anterior = marcador.getLngLat()
  const origem: [number, number] = [anterior.lng, anterior.lat]

  const agoraMs = performance.now()
  const anteriorEm = ultimoPing.get(chave)
  ultimoPing.set(chave, agoraMs)

  // Primeira posição, ou salto grande demais (recuperou GPS depois de um túnel,
  // trocou de aparelho): aparece direto, porque animar isso desenharia uma
  // viagem que não aconteceu.
  const distancia = Math.hypot(destino[0] - origem[0], destino[1] - origem[1])
  if (distancia === 0 || distancia > 0.02) {
    marcador.setLngLat(destino)
    return
  }

  const duracao =
    anteriorEm === undefined
      ? DESLIZE_PADRAO_MS
      : Math.min(Math.max(agoraMs - anteriorEm, DESLIZE_MINIMO_MS), DESLIZE_MAXIMO_MS)

  const anteriorEmCurso = animacoes.get(chave)
  if (anteriorEmCurso !== undefined) cancelAnimationFrame(anteriorEmCurso)

  const inicio = performance.now()

  const passo = (agora: number) => {
    const f = Math.min((agora - inicio) / duracao, 1)

    marcador.setLngLat([
      origem[0] + (destino[0] - origem[0]) * f,
      origem[1] + (destino[1] - origem[1]) * f,
    ])

    if (f < 1) {
      animacoes.set(chave, requestAnimationFrame(passo))
      return
    }

    animacoes.delete(chave)
  }

  animacoes.set(chave, requestAnimationFrame(passo))
}

// Conteúdo dos balões. DOM montado na mão, nunca setHTML com interpolação:
// nome de cliente e endereço vêm do banco e não podem virar markup.
interface LinhaDoBalao {
  rotulo: string
  valor: string
  destaque?: boolean
}

// Etiqueta vazia não desenha nada: no balão da loja o nome já diz o que é
// aquele ponto, e a tarja em cima só repetia o óbvio.
function montarBalao(
  etiqueta: string,
  titulo: string,
  linhas: readonly LinhaDoBalao[],
  classeEtiqueta: string,
): HTMLElement {
  const caixa = criarMarcador('flex w-56 flex-col gap-2 p-3')

  const topo = criarMarcador('flex flex-col gap-1')

  const nome = document.createElement('strong')
  nome.className = 'text-corpo font-semibold text-texto'
  nome.textContent = titulo

  if (etiqueta) {
    const eyebrow = document.createElement('span')
    eyebrow.className = `self-start rounded-controle px-1.5 py-0.5 text-rotulo font-bold uppercase ${classeEtiqueta}`
    eyebrow.textContent = etiqueta
    topo.appendChild(eyebrow)
  }

  topo.appendChild(nome)
  caixa.appendChild(topo)

  const lista = document.createElement('dl')
  lista.className = 'flex flex-col gap-1'

  for (const linha of linhas) {
    if (!linha.valor) continue

    const par = criarMarcador('flex items-baseline justify-between gap-3')

    const rotulo = document.createElement('dt')
    rotulo.className = 'shrink-0 text-rotulo uppercase text-texto-fraco'
    rotulo.textContent = linha.rotulo

    const valor = document.createElement('dd')
    valor.className = linha.destaque
      ? 'text-right text-apoio font-semibold text-texto'
      : 'text-right text-apoio text-texto-suave'
    valor.textContent = linha.valor

    par.append(rotulo, valor)
    lista.appendChild(par)
  }

  if (lista.childElementCount > 0) caixa.appendChild(lista)

  return caixa
}

function balaoDaLoja(nome: string, endereco: string): HTMLElement {
  return montarBalao('', nome || 'Sua loja', [
    { rotulo: 'Endereço', valor: endereco },
  ], '')
}

function balaoDoMotoboy(p: PosicaoEntregador): HTMLElement {
  if (!p.pedidoId) {
    return montarBalao('Motoboy', p.entregadorNome, [
      { rotulo: 'Situação', valor: 'Online, sem entrega' },
    ], 'bg-superficie-afundada text-texto-suave')
  }

  return montarBalao('Em entrega', p.entregadorNome, [
    { rotulo: 'Pedido', valor: `#${p.pedidoNumero ?? ''}`, destaque: true },
    { rotulo: 'Situação', valor: p.pedidoStatus === 'Chegou' ? 'No local' : 'A caminho' },
    { rotulo: 'Cliente', valor: p.clienteNome ?? '' },
    { rotulo: 'Endereço', valor: p.enderecoResumido ?? '' },
  ], 'bg-marca-50 text-marca-600')
}

function balaoDoPedido(pedido: Pedido, cromo: { pill: string }): HTMLElement {
  return montarBalao(TITULO_COLUNA[pedido.status], `#${pedido.numeroExibicao}`, [
    { rotulo: 'Cliente', valor: pedido.clienteNome },
    { rotulo: 'Endereço', valor: pedido.enderecoResumido ?? '' },
    { rotulo: 'Total', valor: formatarDinheiro(pedido.valorTotal), destaque: true },
    pedido.pagamentoValorACobrar > 0
      ? { rotulo: 'A cobrar', valor: formatarDinheiro(pedido.pagamentoValorACobrar), destaque: true }
      : { rotulo: 'Pagamento', valor: pedido.pagamentoDescricao },
    { rotulo: 'Motoboy', valor: pedido.entregadorNome ?? '' },
  ], cromo.pill)
}

interface PontoDaLoja {
  latitude: number
  longitude: number
  /** Endereço já resumido para exibição, vindo de useEnderecoDaLoja. */
  resumo?: string
}

interface MapaEntregaProps {
  /** Uma posição por motoboy online. */
  posicoes: PosicaoEntregador[]
  /** Pedidos em aberto: cada um vira um balão no endereço de entrega. */
  pedidos?: Pedido[]
  /** Ponto fixo da loja — é de onde a entrega sai. */
  loja?: PontoDaLoja | null
  /** Nome exibido no balão da loja. */
  nomeDaLoja?: string | null
  className?: string
}

export function MapaEntrega({
  posicoes,
  pedidos = [],
  loja,
  nomeDaLoja,
  className,
}: MapaEntregaProps) {
  const divRef = useRef<HTMLDivElement>(null)
  const mapaRef = useRef<MapLibreMap | null>(null)
  const prontoRef = useRef(false)
  // Um marcador por motoboy, indexado pelo id dele.
  const motoboysRef = useRef<Map<string, Marker>>(new Map())
  // Um balão por pedido, com o estado que ele tinha quando foi desenhado —
  // é o que diz se a cor precisa ser refeita.
  const pedidosRef = useRef<Map<string, { marcador: Marker; estado: StatusPedido }>>(new Map())
  const lojaRef = useRef<Marker | null>(null)
  // requestAnimationFrame em curso por motoboy, para cancelar o deslize
  // anterior quando chega ping novo e ao desmontar.
  const animacoesRef = useRef<Map<string, number>>(new Map())
  // Instante do último ping por motoboy: é dele que sai a duração do deslize.
  const ultimoPingRef = useRef<Map<string, number>>(new Map())
  const seguiuRef = useRef(false)

  useEffect(() => {
    if (!divRef.current || mapaRef.current) return

    // Capturado aqui porque a limpeza abaixo o usa: a instância do Map nunca
    // troca, mas ler .current dentro do cleanup dispara aviso do linter.
    const marcadoresDeMotoboy = motoboysRef.current
    const baloesDePedido = pedidosRef.current
    const animacoesEmCurso = animacoesRef.current

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
      // Frame pendente sobre marcador de mapa destruído estoura ao rodar.
      for (const frame of animacoesEmCurso.values()) cancelAnimationFrame(frame)
      animacoesEmCurso.clear()

      marcadoresDeMotoboy.clear()
      baloesDePedido.clear()
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
      element: criarPino(pinoLojaUrl, 'Loja'),
      anchor: 'bottom',
      offset: DESLOCAMENTO_DA_PONTA,
      pitchAlignment: 'viewport',
      rotationAlignment: 'viewport',
    })
      .setLngLat(ponto)
      .setPopup(
        new Popup({ offset: 16, closeButton: false, maxWidth: 'none' }).setDOMContent(
          balaoDaLoja(nomeDaLoja ?? '', loja.resumo ?? ''),
        ),
      )
      .addTo(mapa)
  }, [loja, nomeDaLoja])

  // Um balão por pedido, no endereço de entrega dele.
  useEffect(() => {
    const mapa = mapaRef.current
    if (!mapa) return

    const vistos = new Set<string>()

    for (const pedido of pedidos) {
      // Pedido sem coordenada existe e aparece no Kanban — só não tem onde
      // ser desenhado. O sandbox do iFood manda 0,0 justamente assim.
      if (pedido.enderecoLatitude == null || pedido.enderecoLongitude == null) continue

      vistos.add(pedido.id)
      const ponto: [number, number] = [pedido.enderecoLongitude, pedido.enderecoLatitude]
      const existente = pedidosRef.current.get(pedido.id)

      // A cor muda junto com o status, então o balão é refeito quando o estado
      // avança — trocar a classe do elemento existente daria no mesmo, mas
      // recriar mantém um único lugar decidindo como o balão é montado.
      if (existente) {
        if (existente.estado === pedido.status) {
          existente.marcador.setLngLat(ponto)
          // O detalhe é refeito a cada atualização: o que está a cobrar muda
          // quando o motoboy recebe, e o balão precisa contar a verdade atual.
          existente.marcador
            .getPopup()
            ?.setDOMContent(balaoDoPedido(pedido, CROMO_DA_COLUNA[pedido.status]))
          continue
        }

        existente.marcador.remove()
      }

      const elemento = criarBalaoDePedido(pedido.numeroExibicao, CROMO_DA_COLUNA[pedido.status])
      const balao = new Popup({ offset: 16, closeButton: false, maxWidth: 'none' }).setDOMContent(
        balaoDoPedido(pedido, CROMO_DA_COLUNA[pedido.status]),
      )
      const marcador = new Marker({
        // Âncora embaixo: a ponta do balão é que encosta na coordenada.
        anchor: 'bottom',
        element: elemento,
      })
        .setLngLat(ponto)
        .setPopup(balao)
        .addTo(mapa)

      // Mesmo gesto do marcador do motoboy: passar o mouse mostra o detalhe,
      // sem exigir clique nem tirar o dono do que ele estava fazendo.
      elemento.addEventListener('mouseenter', () => marcador.togglePopup())
      elemento.addEventListener('mouseleave', () => marcador.togglePopup())

      pedidosRef.current.set(pedido.id, { marcador, estado: pedido.status })
    }

    // Pedido que saiu da lista (concluído, cancelado) sai do mapa junto.
    for (const [id, item] of pedidosRef.current) {
      if (vistos.has(id)) continue
      item.marcador.remove()
      pedidosRef.current.delete(id)
    }
  }, [pedidos])

  // Um marcador por motoboy.
  useEffect(() => {
    const mapa = mapaRef.current
    if (!mapa || posicoes.length === 0) return

    const vistos = new Set<string>()

    for (const posicao of posicoes) {
      vistos.add(posicao.entregadorId)
      const ponto: [number, number] = [posicao.longitude, posicao.latitude]
      const existente = motoboysRef.current.get(posicao.entregadorId)

      if (existente) {
        // Desliza até a posição nova em vez de saltar — ver `deslizarMarcador`.
        deslizarMarcador(
          existente,
          ponto,
          animacoesRef.current,
          ultimoPingRef.current,
          posicao.entregadorId,
        )
        // O balão é refeito a cada ping porque o status do pedido muda no
        // meio da entrega — "a caminho" vira "no local".
        existente.getPopup()?.setDOMContent(balaoDoMotoboy(posicao))
        continue
      }

      const elemento = criarPino(pinoMotoboyUrl, posicao.entregadorNome)
      const balao = new Popup({ offset: 22, closeButton: false, maxWidth: 'none' }).setDOMContent(
        balaoDoMotoboy(posicao),
      )
      const marcador = new Marker({
        element: elemento,
        // A ponta do pino é que toca a coordenada.
        anchor: 'bottom',
        offset: DESLOCAMENTO_DA_PONTA,
        // Em pé, sempre: sem isto o marcador deita junto com a inclinação da
        // câmera e a rotação do mapa, e o ícone aparece tombado sobre a rua.
        pitchAlignment: 'viewport',
        rotationAlignment: 'viewport',
      })
        .setLngLat(ponto)
        .setPopup(balao)
        .addTo(mapa)

      // Abre ao passar o mouse, não só no clique: o dono quer conferir de
      // relance quem é e para onde vai, sem perder o que estava fazendo.
      elemento.addEventListener('mouseenter', () => marcador.togglePopup())
      elemento.addEventListener('mouseleave', () => marcador.togglePopup())

      motoboysRef.current.set(posicao.entregadorId, marcador)
    }

    // Quem ficou offline some do mapa.
    for (const [id, marcador] of motoboysRef.current) {
      if (vistos.has(id)) continue

      const frame = animacoesRef.current.get(id)
      if (frame !== undefined) cancelAnimationFrame(frame)
      animacoesRef.current.delete(id)
      ultimoPingRef.current.delete(id)

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

  }, [posicoes])

  return <div ref={divRef} className={className} />
}
