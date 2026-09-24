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
  COLUNAS_ENTREGADOR,
  podeMoverPara,
  proximoPasso,
  proximoPassoEntregador,
} from '@/dominio/pedido'
import { useAgora } from '@/lib/tempo'
import { useMotoboys } from '@/motoboys/useMotoboys'
import { FaixaResumo } from '@/dashboard/FaixaResumo'
import { AlternadorDisponibilidade } from '@/entregador/AlternadorDisponibilidade'
import { useDisponibilidade } from '@/entregador/useDisponibilidade'
import { useTelaAcesa } from '@/entregador/useTelaAcesa'
import { STATUS_EM_ENTREGA } from '@/dominio/pedido'
import { CartaoPedido } from './CartaoPedido'
import { ColunaPedidos } from './ColunaPedidos'
import { FormularioPedidoInterno } from './FormularioPedidoInterno'
import { EtiquetaGps } from './EtiquetaGps'
import { MapaEntrega } from './MapaEntrega'
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
  const { pedidos, carregando, erro, mover, alocar, cancelar, recarregar } = usePedidos()
  const [lancando, setLancando] = useState(false)
  const { usuario } = useSessao()
  const { entregadores } = useMotoboys()
  const entregadoresAtivos = entregadores.filter((e) => e.status === 'Ativo')
  const { registrar } = useNovoPedido()

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

  return (
    <div className="flex flex-col gap-4">
      <FaixaResumo />

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

      <Kanban
        pedidos={pedidos}
        carregando={carregando}
        erro={erro}
        colunas={COLUNAS}
        podeMoverParaFn={podeMoverPara}
        obterProximoPasso={proximoPasso}
        onMover={mover}
        entregadoresAtivos={entregadoresAtivos}
        onAlocar={alocar}
        onCancelar={cancelar}
        posicoes={posicoes}
      />

      {/* Mapa da operação: a loja fica fixa e o motoboy se move em tempo real
          enquanto houver entrega em curso.

          Cartão externo com o mesmo gradiente/borda do "Taxa por entrega" da
          FaixaResumo — a moldura vive aqui, envolvendo cabeçalho e mapa. Sem
          Cartao/CartaoCorpo pra não brigar com o próprio border/background
          padrão do componente. */}
      <div
        className="rounded-[14px] p-3.5"
        style={{
          background:
            'linear-gradient(135deg, rgba(79,70,229,0.22), rgba(79,70,229,0.06))',
          border: '1px solid rgba(79,70,229,0.35)',
        }}
      >
        <div className="flex flex-col gap-3">
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <h2 className="text-titulo text-texto">Mapa da operação</h2>
            <span className="text-apoio text-texto-suave">
              {emRota
                ? `Acompanhando #${emRota.numeroExibicao}${emRota.entregadorNome ? ` · ${emRota.entregadorNome}` : ''}`
                : 'Nenhuma entrega em rota agora'}
            </span>
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

function PainelEntregador() {
  const { pedidos, carregando, erro, mover } = useMinhasEntregas()
  const emEntrega = pedidos.some((p) => p.entregadorId && STATUS_EM_ENTREGA.includes(p.status))

  // Enquanto o motoboy está online — com entrega ou esperando —, o navegador
  // dele emite GPS. Ele não vê mapa aqui, mas continua emitindo: é isso que
  // alimenta o mapa do restaurante.
  const { disponivel, erro: erroDisponibilidade, definir } = useDisponibilidade()
  const { estado: estadoGps } = useEnviarPosicao(pedidos, disponivel)

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
function ListaDeEntregas({
  pedidos,
  carregando,
  erro,
  onMover,
}: {
  pedidos: Pedido[]
  carregando: boolean
  erro: string | null
  onMover: (pedido: Pedido, destino: StatusPedido) => void
}) {
  const agora = useAgora()

  useAlertaSonoro(pedidos, agora)

  if (carregando) {
    return <p className="p-4 text-corpo text-texto-suave">Carregando entregas…</p>
  }

  // Na ordem em que a entrega anda, não por chegada: o que está mais perto de
  // terminar aparece primeiro.
  const emOrdem = [...pedidos].sort(
    (a, b) => COLUNAS_ENTREGADOR.indexOf(b.status) - COLUNAS_ENTREGADOR.indexOf(a.status),
  )

  return (
    // w-72 é a mesma largura da coluna do Kanban: o cartão mantém a proporção
    // que já tinha, em vez de esticar até a borda da tela.
    //
    // Alinhado à esquerda, sob o alternador Online/Offline. Centralizado, numa
    // tela larga o cartão flutuava sozinho no meio do vazio, desencostado do
    // controle que manda nele.
    <div className="flex flex-col items-start gap-3">
      {erro && (
        <Cartao className="w-72 border-perigo">
          <CartaoCorpo className="flex items-center gap-3">
            <Etiqueta tom="alerta">Erro</Etiqueta>
            <span className="text-corpo text-texto">{erro}</span>
          </CartaoCorpo>
        </Cartao>
      )}

      {emOrdem.length === 0 ? (
        <p className="px-2 py-8 text-center text-apoio text-texto-fraco">
          Nenhuma entrega no momento.
        </p>
      ) : (
        emOrdem.map((pedido) => (
          <CartaoPedido
            key={pedido.id}
            pedido={pedido}
            agora={agora}
            arrastavel={false}
            mostrarEstado
            mostrarNavegacao
            className="w-72"
            obterProximoPasso={proximoPassoEntregador}
            onAvancar={(p) => {
              const passo = proximoPassoEntregador(p)
              if (passo) onMover(p, passo.destino)
            }}
          />
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
  posicoes,
}: KanbanProps) {
  const agora = useAgora()
  const [arrastando, setArrastando] = useState<Pedido | null>(null)

  useAlertaSonoro(pedidos, agora)

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
