import { DndContext, DragOverlay, PointerSensor, useSensor, useSensors } from '@dnd-kit/core'
import type { DragEndEvent, DragStartEvent } from '@dnd-kit/core'
import { useEffect, useState } from 'react'
import { useSessao } from '@/auth/SessaoProvider'
import { Botao } from '@/components/Botao'
import { Cartao, CartaoCorpo } from '@/components/Cartao'
import { Etiqueta } from '@/components/Etiqueta'
import type { Entregador } from '@/dominio/entregador'
import type { ColunaDoQuadro, ObterProximoPasso, Pedido, StatusPedido } from '@/dominio/pedido'
import {
  COLUNAS,
  podeMoverPara,
  proximoPasso,
  proximoPassoEntregador,
} from '@/dominio/pedido'
import { useAgora } from '@/lib/tempo'
import { useMotoboys } from '@/motoboys/useMotoboys'
import { AlternadorDisponibilidade } from '@/entregador/AlternadorDisponibilidade'
import { useDisponibilidade } from '@/entregador/useDisponibilidade'
import { useTelaAcesa } from '@/entregador/useTelaAcesa'
import { STATUS_EM_ENTREGA } from '@/dominio/pedido'
import { BarraFiltros, type CanalFiltro, type PeriodoFiltro, type VisaoKanban } from './BarraFiltros'
import { CartaoPedido } from './CartaoPedido'
import { ColunaPedidos } from './ColunaPedidos'
import { FaixaKpis } from './FaixaKpis'
import { FormularioPedidoInterno } from './FormularioPedidoInterno'
import { PaneConcluidos } from './PaneConcluidos'
import { EtiquetaGps } from './EtiquetaGps'
import { IndicadorDeParada } from './IndicadorDeParada'
import { MapaEntrega } from './MapaEntrega'
import { montarCorrida } from './rotaDoMotoboy'
import type { PontoDeReferencia } from './rotaDoMotoboy'
import { useAlertaSonoro } from './useAlertaSonoro'
import { useEnderecoDaLoja } from './useEnderecoDaLoja'
import { useEnviarPosicao } from './useEnviarPosicao'
import { useMinhasEntregas } from './useMinhasEntregas'
import { usePedidos } from './usePedidos'
import { useRastreio } from './useRastreio'
import { useNovoPedido } from './NovoPedidoContext'
import type { PosicaoEntregador } from './useRastreio'

// Mesmo Kanban pros dois papéis — só muda a fonte de dados e as ações
// (dono avança o pedido pela cozinha, motoboy avança pela entrega).
export function PainelOperacao() {
  const { usuario } = useSessao()

  return usuario?.papel === 'Entregador' ? <PainelEntregador /> : <PainelDono />
}

function PainelDono() {
  const { pedidos, carregando, erro, mover, alocar, cancelar, despacharLote, recarregar } = usePedidos()
  const [lancando, setLancando] = useState(false)
  const { usuario } = useSessao()
  const { entregadores } = useMotoboys()
  const entregadoresAtivos = entregadores.filter((e) => e.status === 'Ativo')
  const { registrar } = useNovoPedido()

  // Pedidos casados: seleção de vários "Prontos" para sair juntos com um
  // motoboy. O sistema calcula a ordem das paradas no backend.
  const [loteSelecao, setLoteSelecao] = useState<Set<string>>(new Set())
  const [motoboyLote, setMotoboyLote] = useState('')
  const [despachandoLote, setDespachandoLote] = useState(false)
  const [erroLote, setErroLote] = useState<string | null>(null)

  function alternarLote(pedido: Pedido) {
    setErroLote(null)
    setLoteSelecao((atual) => {
      const proximo = new Set(atual)
      if (proximo.has(pedido.id)) proximo.delete(pedido.id)
      else proximo.add(pedido.id)
      return proximo
    })
  }

  function limparLote() {
    setLoteSelecao(new Set())
    setErroLote(null)
  }

  async function despacharLoteAgora() {
    if (!motoboyLote || loteSelecao.size < 2) return
    setDespachandoLote(true)
    const erro = await despacharLote(motoboyLote, [...loteSelecao])
    setDespachandoLote(false)
    if (erro) {
      setErroLote(erro)
      return
    }
    setLoteSelecao(new Set())
    setMotoboyLote('')
  }

  // Registra o callback para o botão do header poder abrir o formulário.
  // useEffect garante re-registro se o componente remontar.
  useEffect(() => {
    registrar(() => setLancando(true))
  }, [registrar])

  // Uma única conexão SignalR por sessão do dono — recebe pings de todos os
  // pedidos da loja e o Kanban filtra pelo pedido de cada cartão.
  const { posicoes } = useRastreio(usuario?.merchantId)
  const loja = useEnderecoDaLoja(usuario?.merchantId)

  const emRota = pedidos.find((p) => p.status === 'EmRota' || p.status === 'Chegou')
  const agoraMs = useAgora()

  // Filtros do topo: puramente de visualização, não disparam novo fetch (a
  // janela "hoje" já vem do backend). Lista local do busca/canal pra não
  // esperar roundtrip em cada tecla.
  const [busca, setBusca] = useState('')
  const [periodo, setPeriodo] = useState<PeriodoFiltro>('hoje')
  const [canal, setCanal] = useState<CanalFiltro>('todos')
  const [visao, setVisao] = useState<VisaoKanban>('kanban')

  const buscaMinuscula = busca.trim().toLowerCase()
  const pedidosFiltrados = pedidos.filter((p) => {
    if (canal !== 'todos' && p.origem !== canal) return false
    if (!buscaMinuscula) return true
    return (
      p.numeroExibicao.toLowerCase().includes(buscaMinuscula) ||
      p.clienteNome.toLowerCase().includes(buscaMinuscula) ||
      (p.codigoDeEntrega?.toLowerCase().includes(buscaMinuscula) ?? false)
    )
  })

  // Finalizados saíram do Kanban pra pane à direita — hoje é 6 etapas visíveis
  // + 1 pane. Mantemos o rótulo "7 etapas" na seção do fluxo porque o pedido
  // continua passando pelas 7 (Concluído é o destino).
  const colunasKanban = COLUNAS.filter((c) => c.id !== 'finalizados')

  return (
    <div className="flex h-full flex-col gap-4">
      {/* Área que rola: hero, KPIs, filtros, quadro + pane. O mapa fica
          fora dela, encaixado no rodapé (shrink-0) — sempre visível. */}
      <div className="flex min-h-0 flex-1 flex-col gap-4 overflow-y-auto">
        {/* Hero: título da página, subtítulo e ação primária. O "Novo pedido"
            da topbar foi removido pra concentrar aqui — a tela faz sentido
            fora da moldura global. */}
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="flex min-w-0 flex-col">
            <h1 className="text-[26px] font-bold leading-tight tracking-tight text-texto">
              Gestão de pedidos
            </h1>
            <p className="text-apoio text-texto-suave">
              Acompanhe a operação, do recebimento à finalização.
            </p>
          </div>
          <Botao onClick={() => setLancando(true)}>
            <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round">
              <path d="M12 5v14M5 12h14" />
            </svg>
            Novo pedido
          </Botao>
        </div>

        <FaixaKpis pedidos={pedidos} />

        <BarraFiltros
          busca={busca}
          onBusca={setBusca}
          periodo={periodo}
          onPeriodo={setPeriodo}
          canal={canal}
          onCanal={setCanal}
          visao={visao}
          onVisao={setVisao}
        />

      {/* Venda de balcão, telefone ou WhatsApp: entra pelo mesmo quadro dos
          pedidos do iFood, só não tem marketplace para avisar. */}
      {lancando ? (
        <div className="flex flex-col gap-2">
          <div className="flex items-center justify-between gap-2">
            <h2 className="text-titulo text-texto">Novo pedido</h2>
            <Botao variante="secundario" onClick={() => setLancando(false)}>
              Cancelar
            </Botao>
          </div>
          <FormularioPedidoInterno
            onLancado={() => {
              setLancando(false)
              void recarregar()
            }}
          />
        </div>
      ) : null}

      {/* Barra de despacho em lote: aparece quando há pedidos marcados. Escolhe
          o motoboy e despacha todos numa corrida — o sistema calcula a ordem
          das paradas (pedido mais próximo primeiro). */}
      {loteSelecao.size > 0 && (
        <div className="flex flex-wrap items-center gap-3 rounded-[14px] border border-marca-300 bg-marca-50 px-4 py-3">
          <span className="text-corpo font-semibold text-marca-700">
            {loteSelecao.size} {loteSelecao.size === 1 ? 'pedido selecionado' : 'pedidos selecionados'}
          </span>
          <select
            value={motoboyLote}
            onChange={(e) => setMotoboyLote(e.target.value)}
            className="rounded-controle border border-borda-forte bg-superficie px-3 py-2 text-apoio text-texto"
          >
            <option value="">Escolha o motoboy…</option>
            {entregadoresAtivos.map((e) => (
              <option key={e.courierId} value={e.courierId}>
                {e.nome}
              </option>
            ))}
          </select>
          <Botao
            onClick={despacharLoteAgora}
            disabled={loteSelecao.size < 2 || !motoboyLote}
            carregando={despachandoLote}
          >
            Despachar juntos
          </Botao>
          <Botao variante="secundario" onClick={limparLote} disabled={despachandoLote}>
            Limpar
          </Botao>
          {loteSelecao.size < 2 && (
            <span className="text-apoio text-texto-suave">Selecione ao menos 2 para casar.</span>
          )}
          {erroLote && <span className="text-apoio font-medium text-perigo">{erroLote}</span>}
        </div>
      )}

      {/* Título da seção do fluxo + atalho semântico (apenas visual por ora).
          A contagem "7 etapas" bate com o domínio: 6 colunas visíveis + Concluído
          no pane à direita. */}
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <div className="flex items-baseline gap-2">
          <h2 className="text-titulo font-semibold text-texto">Fluxo de pedidos</h2>
          <span className="text-apoio text-texto-fraco">7 etapas</span>
        </div>
        <span className="text-apoio text-texto-suave">Do recebimento à finalização →</span>
      </div>

      {/* Grid principal: Kanban à esquerda (cresce), Concluídos à direita (fixo
          em lg+, embaixo em telas estreitas). min-h-0 pra o Kanban conseguir
          rolar lateralmente no mobile em vez de empurrar o layout. */}
      <div className="flex min-h-0 flex-col gap-4 lg:flex-row">
        <div className="min-w-0 flex-1">
          {visao === 'kanban' ? (
            <Kanban
              pedidos={pedidosFiltrados.filter((p) => p.status !== 'Concluido')}
              carregando={carregando}
              erro={erro}
              colunas={colunasKanban}
              podeMoverParaFn={podeMoverPara}
              obterProximoPasso={proximoPasso}
              onMover={mover}
              entregadoresAtivos={entregadoresAtivos}
              onAlocar={alocar}
              onCancelar={cancelar}
              onAlternarLote={alternarLote}
              selecaoLote={loteSelecao}
              posicoes={posicoes}
            />
          ) : (
            <ListaDePedidos
              pedidos={pedidosFiltrados.filter((p) => p.status !== 'Concluido')}
              agora={agoraMs}
              onAvancar={(p) => {
                const passo = proximoPasso(p)
                if (passo) mover(p, passo.destino)
              }}
              onCancelar={cancelar}
              posicoes={posicoes}
            />
          )}
        </div>
        <PaneConcluidos pedidos={pedidosFiltrados} agora={agoraMs} />
      </div>
      </div>

      {/* Mapa da operação: encaixado no rodapé do conteúdo (shrink-0), sempre
          visível, com os pedidos rolando acima. A loja fica fixa e o motoboy
          se move em tempo real enquanto houver entrega em curso. Cartão externo
          com o mesmo gradiente/borda do "Taxa por entrega" da FaixaResumo. */}
      <div className="shrink-0 rounded-[14px] border border-borda bg-transparent p-3.5">
        <div className="flex flex-col gap-3">
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <div className="flex items-baseline gap-3">
              <h2 className="text-titulo text-texto">Mapa da operação</h2>
              <span className="text-apoio text-texto-suave">
                {emRota
                  ? `Acompanhando #${emRota.numeroExibicao}${emRota.entregadorNome ? ` · ${emRota.entregadorNome}` : ''}`
                  : 'Nenhuma entrega em rota agora'}
              </span>
            </div>
            <div className="flex flex-wrap items-center gap-3 text-apoio text-texto-fraco">
              <LegendaChip cor="bg-estado-preparo" rotulo="Em preparo" />
              <LegendaChip cor="bg-estado-emrota" rotulo="Em rota" />
              <LegendaChip cor="bg-estado-concluido" rotulo="Finalizado" />
            </div>
          </div>

          {loja ? (
            // resize é do navegador — mesma alça de arrastar de um <textarea>.
            // O MapLibre já observa o próprio container por ResizeObserver
            // (trackResize por padrão), então basta o CSS: nenhum JavaScript
            // extra pra ele redesenhar. O wrapper existe só pra hospedar a
            // alça de arrastar e a alça visível — nada mais.
            <div className="relative h-96 w-full min-h-64 min-w-72 resize overflow-hidden rounded-[10px]">
              <MapaEntrega
                posicoes={posicoes}
                pedidos={pedidos}
                loja={loja}
                nomeDaLoja={usuario?.nomeRestaurante}
                className="size-full"
              />
              {/* Alça visível — dois traços diagonais sobre a alça nativa do
                  navegador. pointer-events-none pra não capturar o mouse: quem
                  redimensiona é a alça nativa embaixo, esta camada é só um
                  aviso visual de que aquele canto agarra. */}
              <span
                aria-hidden
                className="pointer-events-none absolute bottom-1 right-1 size-3.5 opacity-80"
                style={{
                  background:
                    'linear-gradient(135deg, transparent 0 45%, rgba(199,203,255,0.9) 45% 55%, transparent 55% 70%, rgba(199,203,255,0.9) 70% 80%, transparent 80%)',
                }}
              />
            </div>
          ) : (
            <p className="py-8 text-center text-apoio text-texto-fraco">
              Cadastre o endereço da loja para ancorar o mapa.
            </p>
          )}
        </div>
      </div>
    </div>
  )
}

function LegendaChip({ cor, rotulo }: { cor: string; rotulo: string }) {
  return (
    <span className="inline-flex items-center gap-1.5">
      <span aria-hidden className={`size-2 rounded-full ${cor}`} />
      {rotulo}
    </span>
  )
}

// Visão alternativa ao Kanban: lista única com os cards empilhados. Mesmo
// CartaoPedido e mesmas ações — só muda a geometria. Útil pra ver a fila
// inteira sem decidir por etapa.
function ListaDePedidos({
  pedidos,
  agora,
  onAvancar,
  onCancelar,
  posicoes,
}: {
  pedidos: Pedido[]
  agora: number
  onAvancar: (pedido: Pedido) => void
  onCancelar: (pedido: Pedido, motivo: string) => Promise<string | null>
  posicoes: PosicaoEntregador[]
}) {
  if (pedidos.length === 0) {
    return (
      <div className="rounded-cartao border border-borda bg-superficie p-6 text-center text-apoio text-texto-fraco">
        Nenhum pedido ativo no filtro atual.
      </div>
    )
  }
  return (
    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-3">
      {pedidos.map((p) => (
        <CartaoPedido
          key={p.id}
          pedido={p}
          agora={agora}
          arrastavel={false}
          mostrarEstado
          onAvancar={onAvancar}
          onCancelar={onCancelar}
          posicoes={posicoes}
        />
      ))}
    </div>
  )
}

function PainelEntregador() {
  const { pedidos, carregando, erro, mover } = useMinhasEntregas()
  const emEntrega = pedidos.some((p) => p.entregadorId && STATUS_EM_ENTREGA.includes(p.status))

  // Enquanto o motoboy está online — com entrega ou esperando —, o navegador
  // dele emite GPS. Ele não vê mapa aqui, mas continua emitindo: é isso que
  // alimenta o mapa do restaurante.
  const { disponivel, erro: erroDisponibilidade, definir } = useDisponibilidade()
  const { estado: estadoGps, posicaoAtual } = useEnviarPosicao(pedidos, disponivel)

  // O Wake Lock reduz o risco de o navegador congelar o GPS, mas não é
  // requisito para continuar emitindo posição.
  const { estado: estadoTela, abaVisivel } = useTelaAcesa(disponivel === true || emEntrega)
  const avisoTela =
    estadoTela === 'indisponivel' || estadoTela === 'erro' || estadoTela === 'perdida' || !abaVisivel

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-3">
        <AlternadorDisponibilidade
          disponivel={disponivel}
          erro={erroDisponibilidade}
          definir={definir}
        />
        {emEntrega && <Etiqueta tom="sucesso">Em entrega agora</Etiqueta>}
        <EtiquetaGps estado={estadoGps} />
        {avisoTela && (
          <Etiqueta tom="alerta">
            Mantenha o aplicativo aberto para melhorar o rastreamento.
          </Etiqueta>
        )}
      </div>

      <ListaDeEntregas
        pedidos={pedidos}
        carregando={carregando}
        erro={erro}
        onMover={mover}
        posicaoAtual={posicaoAtual}
      />
    </div>
  )
}

// O motoboy usa o celular na rua, de capacete e com pressa: quadro de quatro
// colunas com arraste lateral não serve. Uma coluna só, e o cartão muda de
// etapa no lugar — o botão vira o próximo passo a cada clique.
//
// Sem mapa aqui de propósito: a navegação acontece no Waze ou no Google Maps,
// pelos botões do próprio cartão. O GPS continua sendo emitido (useEnviarPosicao
// segue ativo acima), porque é ele que alimenta o mapa do restaurante.
// Monta a URL de rota multi-parada (Google Maps) da corrida atual — o primeiro
// lote encontrado nos pedidos do motoboy, com as paradas na ordem calculada.
// Precisa de 2+ paradas com coordenada; senão não há rota casada a abrir.
function montarRotaDoLote(pedidos: Pedido[]): string | null {
  const primeiroLote = pedidos.find((p) => p.loteEntregaId)?.loteEntregaId
  if (!primeiroLote) return null

  const paradas = pedidos
    .filter(
      (p) =>
        p.loteEntregaId === primeiroLote &&
        p.ordemNaRota != null &&
        p.enderecoLatitude != null &&
        p.enderecoLongitude != null,
    )
    .sort((a, b) => (a.ordemNaRota as number) - (b.ordemNaRota as number))

  if (paradas.length < 2) return null

  const pontos = paradas.map((p) => `${p.enderecoLatitude},${p.enderecoLongitude}`)
  const destino = pontos[pontos.length - 1]
  const waypoints = pontos.slice(0, -1).join('|')

  return `https://www.google.com/maps/dir/?api=1&destination=${destino}&waypoints=${encodeURIComponent(waypoints)}&travelmode=driving`
}

function ListaDeEntregas({
  pedidos,
  carregando,
  erro,
  onMover,
  posicaoAtual,
}: {
  pedidos: Pedido[]
  carregando: boolean
  erro: string | null
  onMover: (pedido: Pedido, destino: StatusPedido) => void
  posicaoAtual: PontoDeReferencia | null
}) {
  const agora = useAgora()

  useAlertaSonoro(pedidos)

  if (carregando) {
    return <p className="p-4 text-corpo text-texto-suave">Carregando entregas…</p>
  }

  // Corrida completa, com paradas numeradas: concluídas primeiro (topo, com
  // tique verde), depois as pendentes na ordem do caminho — mais próxima da
  // posição atual do motoboy vira a "atual", as mais distantes ficam
  // "pendentes". Quando o motoboy conclui a atual, a próxima pendente assume
  // automaticamente, porque a função roda a cada render com a lista nova.
  const corrida = montarCorrida(pedidos, posicaoAtual)

  // Rota multi-parada da corrida atual (primeiro lote encontrado). Abre o Google
  // Maps com as paradas na sequência: waypoints intermediários + destino final.
  const rotaUrl = montarRotaDoLote(pedidos)

  return (
    // Largura fluida com teto: o card estica até ocupar a largura disponível
    // (celular estreito) e trava em max-w-sm (384px) no desktop, pra não
    // flutuar isolado numa faixa vazia. Alinhado à esquerda, sob o alternador
    // Online/Offline — centralizar deixaria o cartão desencostado do controle
    // que manda nele quando a tela é larga.
    <div className="flex w-full max-w-sm flex-col items-stretch gap-3">
      {erro && (
        <Cartao className="border-perigo">
          <CartaoCorpo className="flex items-center gap-3">
            <Etiqueta tom="alerta">Erro</Etiqueta>
            <span className="text-corpo text-texto">{erro}</span>
          </CartaoCorpo>
        </Cartao>
      )}

      {rotaUrl && (
        <Botao
          larguraTotal
          onClick={() => window.open(rotaUrl, '_blank', 'noopener,noreferrer')}
        >
          Abrir rota da corrida
        </Botao>
      )}

      {corrida.length === 0 ? (
        <p className="px-2 py-8 text-center text-apoio text-texto-fraco">
          Nenhuma entrega no momento.
        </p>
      ) : (
        corrida.map(({ pedido, ordem, estado }, i) => (
          // Linha com stepper à esquerda + card à direita. items-stretch no
          // container e flex-1 vertical no indicador fazem o segmento ligar
          // visualmente o número desta parada ao número da próxima — o
          // indicador cresce com a altura do card, mesmo quando ele expande
          // pra mostrar itens. min-w-0 no card permite ele encolher dentro
          // do flex quando o conteúdo (ex.: endereço longo) é maior que a
          // tela do celular.
          <div key={pedido.id} className="flex w-full items-stretch gap-3">
            <IndicadorDeParada
              ordem={ordem}
              estado={corrida.length > 1 ? estado : 'atual'}
              ultima={i === corrida.length - 1}
            />
            <CartaoPedido
              pedido={pedido}
              agora={agora}
              arrastavel={false}
              mostrarEstado
              mostrarNavegacao
              mostrarEntregador={false}
              className="min-w-0 flex-1"
              obterProximoPasso={proximoPassoEntregador}
              onAvancar={(p) => {
                const passo = proximoPassoEntregador(p)
                if (passo) onMover(p, passo.destino)
              }}
            />
          </div>
        ))
      )}
    </div>
  )
}

interface KanbanProps {
  pedidos: Pedido[]
  carregando: boolean
  erro: string | null
  colunas: ColunaDoQuadro[]
  podeMoverParaFn: (pedido: Pedido, destino: StatusPedido) => boolean
  obterProximoPasso: ObterProximoPasso
  onMover: (pedido: Pedido, destino: StatusPedido) => void
  entregadoresAtivos?: Entregador[]
  onAlocar?: (pedido: Pedido, entregadorId: string) => void
  onCancelar?: (pedido: Pedido, motivo: string) => Promise<string | null>
  onAlternarLote?: (pedido: Pedido) => void
  selecaoLote?: Set<string>
  posicoes?: PosicaoEntregador[]
}

function Kanban({
  pedidos,
  carregando,
  erro,
  colunas,
  podeMoverParaFn,
  obterProximoPasso,
  onMover,
  entregadoresAtivos,
  onAlocar,
  onCancelar,
  onAlternarLote,
  selecaoLote,
  posicoes,
}: KanbanProps) {
  const agora = useAgora()
  const [arrastando, setArrastando] = useState<Pedido | null>(null)

  useAlertaSonoro(pedidos)

  // Distância mínima antes de considerar arraste: sem isso, o toque no botão
  // "Confirmar" vira início de arraste e o clique nunca acontece.
  const sensores = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 8 } }))

  function aoIniciar(evento: DragStartEvent) {
    setArrastando(pedidos.find((p) => p.id === evento.active.id) ?? null)
  }

  function aoSoltar(evento: DragEndEvent) {
    setArrastando(null)

    const pedido = pedidos.find((p) => p.id === evento.active.id)
    // O alvo agora é a coluna, não o status: "Em Rota" agrupa quatro status e
    // "Finalizados" nem recebe cartão.
    const destino = colunas.find((c) => c.id === evento.over?.id)?.destino

    // A interface só permite o movimento que a máquina de estados aceita: o
    // pedido anda para frente, um passo por vez.
    if (pedido && destino && podeMoverParaFn(pedido, destino)) {
      onMover(pedido, destino)
    }
  }

  function avancar(pedido: Pedido) {
    const passo = obterProximoPasso(pedido)
    if (passo) onMover(pedido, passo.destino)
  }

  if (carregando) {
    return <p className="p-4 text-corpo text-texto-suave">Carregando pedidos…</p>
  }

  return (
    <div className="flex flex-col gap-4">
      {erro && (
        <Cartao className="border-perigo">
          <CartaoCorpo className="flex items-center gap-3">
            <Etiqueta tom="alerta">Erro</Etiqueta>
            <span className="text-corpo text-texto">{erro}</span>
          </CartaoCorpo>
        </Cartao>
      )}

      <DndContext sensors={sensores} onDragStart={aoIniciar} onDragEnd={aoSoltar}>
        {/* Sem rolagem lateral: as colunas se ajustam à largura disponível.
            Em tela estreita elas quebram para a linha de baixo, que é melhor
            que empurrar metade do quadro para fora da vista. */}
        <div className="flex flex-wrap gap-3">
          {colunas.map((coluna) => (
            <ColunaPedidos
              key={coluna.id}
              coluna={coluna}
              pedidos={pedidos.filter((p) => coluna.status.includes(p.status))}
              agora={agora}
              aceitaSolto={
                arrastando && coluna.destino ? podeMoverParaFn(arrastando, coluna.destino) : false
              }
              onAvancar={avancar}
              obterProximoPasso={obterProximoPasso}
              entregadoresAtivos={entregadoresAtivos}
              onAlocar={onAlocar}
              onCancelar={onCancelar}
              onAlternarLote={onAlternarLote}
              selecaoLote={selecaoLote}
              posicoes={posicoes}
            />
          ))}
        </div>

        {/* O cartão segue o dedo em vez de sumir enquanto arrasta. */}
        <DragOverlay>
          {arrastando && (
            <CartaoPedido
              pedido={arrastando}
              agora={agora}
              onAvancar={() => {}}
              arrastavel={false}
              obterProximoPasso={obterProximoPasso}
            />
          )}
        </DragOverlay>
      </DndContext>
    </div>
  )
}
