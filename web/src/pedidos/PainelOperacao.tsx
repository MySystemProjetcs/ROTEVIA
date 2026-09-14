import { DndContext, DragOverlay, PointerSensor, useSensor, useSensors } from '@dnd-kit/core'
import type { DragEndEvent, DragStartEvent } from '@dnd-kit/core'
import { useState } from 'react'
import { useSessao } from '@/auth/SessaoProvider'
import { Botao } from '@/components/Botao'
import { Cartao, CartaoCorpo } from '@/components/Cartao'
import { Etiqueta } from '@/components/Etiqueta'
import type { Entregador } from '@/dominio/entregador'
import type { ObterProximoPasso, Pedido, StatusPedido } from '@/dominio/pedido'
import {
  COLUNAS,
  COLUNAS_ENTREGADOR,
  podeMoverPara,
  podeMoverParaEntregador,
  proximoPasso,
  proximoPassoEntregador,
} from '@/dominio/pedido'
import { useAgora } from '@/lib/tempo'
import { useMotoboys } from '@/motoboys/useMotoboys'
import { FaixaResumo } from '@/dashboard/FaixaResumo'
import { AlternadorDisponibilidade } from '@/entregador/AlternadorDisponibilidade'
import { useDisponibilidade } from '@/entregador/useDisponibilidade'
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
import type { PosicaoEntregador } from './useRastreio'

// Mesmo Kanban pros dois papéis — só muda a fonte de dados e as ações
// (dono avança o pedido pela cozinha, motoboy avança pela entrega).
export function PainelOperacao() {
  const { usuario } = useSessao()

  return usuario?.papel === 'Entregador' ? <PainelEntregador /> : <PainelDono />
}

function PainelDono() {
  const { pedidos, carregando, erro, mover, alocar, recarregar } = usePedidos()
  const [lancando, setLancando] = useState(false)
  const { usuario } = useSessao()
  const { entregadores } = useMotoboys()
  const entregadoresAtivos = entregadores.filter((e) => e.status === 'Ativo')

  // Uma única conexão SignalR por sessão do dono — recebe pings de todos os
  // pedidos da loja e o Kanban filtra pelo pedido de cada cartão.
  const { posicoes, trilha } = useRastreio(usuario?.merchantId)
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
      ) : (
        <Botao className="self-start" onClick={() => setLancando(true)}>
          Novo pedido
        </Botao>
      )}

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
        posicoes={posicoes}
        trilhaRastreio={trilha}
      />

      {/* Mapa da operação: a loja fica fixa e o motoboy se move em tempo real
          enquanto houver entrega em curso. */}
      <Cartao>
        <CartaoCorpo className="flex flex-col gap-3">
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <h2 className="text-titulo text-texto">Mapa da operação</h2>
            <span className="text-apoio text-texto-suave">
              {emRota
                ? `Acompanhando #${emRota.numeroExibicao}${emRota.entregadorNome ? ` · ${emRota.entregadorNome}` : ''}`
                : 'Nenhuma entrega em rota agora'}
            </span>
          </div>

          {loja ? (
            <MapaEntrega
              posicoes={posicoes}
              trilha={trilha}
              loja={loja}
              className="h-96 w-full overflow-hidden rounded-cartao"
            />
          ) : (
            <p className="py-8 text-center text-apoio text-texto-fraco">
              Cadastre o endereço da loja para ancorar o mapa.
            </p>
          )}
        </CartaoCorpo>
      </Cartao>
    </div>
  )
}

function PainelEntregador() {
  const { pedidos, carregando, erro, mover } = useMinhasEntregas()
  const emEntrega = pedidos.some((p) => p.entregadorId && STATUS_EM_ENTREGA.includes(p.status))

  // Enquanto o motoboy está online — com entrega ou esperando —, o navegador
  // dele emite GPS. É o que alimenta o mapa do restaurante.
  const { disponivel, erro: erroDisponibilidade, definir } = useDisponibilidade()
  const { estado: estadoGps, pedidoEmRota, posicaoAtual, trilha } = useEnviarPosicao(pedidos, disponivel)

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
      </div>

      {pedidoEmRota && (
        <Cartao>
          <CartaoCorpo className="flex flex-col gap-3">
            <div className="flex flex-wrap items-baseline justify-between gap-2">
              <h2 className="text-titulo text-texto">Sua rota</h2>
              <span className="text-apoio text-texto-suave">
                #{pedidoEmRota.numeroExibicao}
                {pedidoEmRota.enderecoResumido ? ` · ${pedidoEmRota.enderecoResumido}` : ''}
              </span>
            </div>

            <MapaEntrega
              posicoes={posicaoAtual ? [posicaoAtual] : []}
              trilha={trilha}
              className="h-80 w-full overflow-hidden rounded-cartao"
            />
          </CartaoCorpo>
        </Cartao>
      )}

      <Kanban
        pedidos={pedidos}
        carregando={carregando}
        erro={erro}
        colunas={COLUNAS_ENTREGADOR}
        podeMoverParaFn={podeMoverParaEntregador}
        obterProximoPasso={proximoPassoEntregador}
        onMover={mover}
      />
    </div>
  )
}

interface KanbanProps {
  pedidos: Pedido[]
  carregando: boolean
  erro: string | null
  colunas: StatusPedido[]
  podeMoverParaFn: (origem: StatusPedido, destino: StatusPedido) => boolean
  obterProximoPasso: ObterProximoPasso
  onMover: (pedido: Pedido, destino: StatusPedido) => void
  entregadoresAtivos?: Entregador[]
  onAlocar?: (pedido: Pedido, entregadorId: string) => void
  posicoes?: PosicaoEntregador[]
  trilhaRastreio?: PosicaoEntregador[]
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
  posicoes,
  trilhaRastreio = [],
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
    const destino = evento.over?.id as StatusPedido | undefined

    // A interface só permite o movimento que a máquina de estados aceita: o
    // pedido anda para frente, um passo por vez.
    if (pedido && destino && podeMoverParaFn(pedido.status, destino)) {
      onMover(pedido, destino)
    }
  }

  function avancar(pedido: Pedido) {
    const passo = obterProximoPasso(pedido.status)
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
        <div className="flex gap-4 overflow-x-auto pb-4">
          {colunas.map((estado) => (
            <ColunaPedidos
              key={estado}
              estado={estado}
              pedidos={pedidos.filter((p) => p.status === estado)}
              agora={agora}
              aceitaSolto={arrastando ? podeMoverParaFn(arrastando.status, estado) : false}
              onAvancar={avancar}
              obterProximoPasso={obterProximoPasso}
              entregadoresAtivos={entregadoresAtivos}
              onAlocar={onAlocar}
              posicoes={posicoes}
              trilhaRastreio={trilhaRastreio}
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
