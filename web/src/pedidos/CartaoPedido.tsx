import { useSortable } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { useState } from 'react'
import { Botao } from '@/components/Botao'
import { Cartao, CartaoCorpo, CartaoRodape } from '@/components/Cartao'
import { Etiqueta } from '@/components/Etiqueta'
import { IconeChevron } from '@/components/icones/IconeChevron'
import { IconeLocal } from '@/components/icones/IconeLocal'
import type { ObterProximoPasso, Pedido, StatusPedido } from '@/dominio/pedido'
import { proximoPasso, temValorACobrar } from '@/dominio/pedido'
import type { Entregador } from '@/dominio/entregador'
import { cn } from '@/lib/cn'
import { formatarDinheiro } from '@/lib/tempo'
import { BotoesDeNavegacao } from './BotoesDeNavegacao'
import { ChipTempoDecorrido } from './ContadorSla'
import { MapaEntrega } from './MapaEntrega'
import type { PosicaoEntregador } from './useRastreio'

// Número do pedido em branco — a cor da etapa já vive na coluna e no cromo
// do mapa (cromoDoStatus). Repetir a cor no cartão lia o número com o fundo
// da coluna e quebra a identidade visual: o pedido deve se destacar como
// elemento, não como espelho da casa onde está.
const COR_NUMERO: Record<StatusPedido, string> = {
  Recebido: 'text-texto',
  Confirmado: 'text-texto',
  EmPreparo: 'text-texto',
  Pronto: 'text-texto',
  Despachado: 'text-texto',
  Aceito: 'text-texto',
  EmRota: 'text-texto',
  Chegou: 'text-texto',
  Cobrar: 'text-texto',
  Concluido: 'text-texto',
  Cancelado: 'text-texto',
}

// Botão de ação na cor da etapa de destino. Sem entrada aqui = variante
// primaria (vale para os passos do motoboy, que não têm cor própria).
type VarianteAcao = 'primario' | 'acaoConfirmar' | 'acaoIniciarPreparo' | 'acaoMarcarPronto' | 'acaoDespachar'

const VARIANTE_ACAO: Partial<Record<StatusPedido, VarianteAcao>> = {
  Confirmado: 'acaoConfirmar',
  EmPreparo: 'acaoIniciarPreparo',
  Pronto: 'acaoMarcarPronto',
  Despachado: 'acaoDespachar',
}

interface CartaoPedidoProps {
  pedido: Pedido
  agora: number
  onAvancar: (pedido: Pedido) => void
  arrastavel?: boolean
  /** Abrir rota no Waze/Maps é ação de quem dirige. O dono acompanha o pedido
   *  pelo mapa da operação; botão de navegação no quadro dele é ruído. */
  mostrarNavegacao?: boolean
  // Fora do Kanban não há coluna dizendo em que etapa o pedido está, então o
  // próprio cartão precisa mostrar. É o caso da lista do motoboy.
  mostrarEstado?: boolean
  className?: string
  obterProximoPasso?: ObterProximoPasso
  // Só o dono recebe estes dois — presença deles é o que liga o par de
  // botões "Alocar Motoboy" / "Despachar" no card Pronto.
  entregadoresAtivos?: Entregador[]
  onAlocar?: (pedido: Pedido, entregadorId: string) => void
  // Posições em tempo real dos motoboys da loja — vêm do PainelDono, que
  // mantém a conexão SignalR. O cartão escolhe a do próprio pedido.
  posicoes?: PosicaoEntregador[]
  // Pedidos casados (só no quadro do dono, em cards Prontos): quando presente,
  // o card mostra uma caixa de seleção para incluir o pedido num despacho em
  // lote. `selecionadoNoLote` reflete o estado da seleção.
  onAlternarLote?: (pedido: Pedido) => void
  selecionadoNoLote?: boolean
  // Só o dono recebe isto — cancelar é ação de gestão da loja, disponível em
  // qualquer status não terminal. Devolve mensagem de erro (ou null) para
  // exibir junto ao campo de motivo.
  onCancelar?: (pedido: Pedido, motivo: string) => Promise<string | null>
  // No cartão do motoboy, o chip "Entrega · <nome>" é redundante: ele sabe
  // que é dele. O dono precisa ver — default true.
  mostrarEntregador?: boolean
}

export function CartaoPedido({
  pedido,
  agora,
  onAvancar,
  arrastavel = true,
  mostrarNavegacao = false,
  className,
  obterProximoPasso = proximoPasso,
  entregadoresAtivos,
  onAlocar,
  posicoes = [],
  onAlternarLote,
  selecionadoNoLote = false,
  onCancelar,
  mostrarEntregador = true,
}: CartaoPedidoProps) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: pedido.id,
    disabled: !arrastavel,
  })
  const [seletorAberto, setSeletorAberto] = useState(false)
  const [cancelamentoAberto, setCancelamentoAberto] = useState(false)
  const [motivoCancelamento, setMotivoCancelamento] = useState('')
  const [enviandoCancelamento, setEnviandoCancelamento] = useState(false)
  const [erroCancelamento, setErroCancelamento] = useState<string | null>(null)
  // O modelo visual de referência apresenta os detalhes do pedido abertos.
  // O usuário ainda pode recolher pelo cabeçalho quando precisar de densidade.
  // Recolhido por padrão: com a grade cheia, cartão mostrando item a item
  // vira parede de texto. Fica só no cartão (não no pedido), então cada card
  // lembra seu próprio estado entre as atualizações de 4s do polling — o
  // React mantém a mesma instância porque a lista é chaveada por pedido.id.
  const [expandido, setExpandido] = useState(true)

  const passo = obterProximoPasso(pedido)
  const precisaCobrar = temValorACobrar(pedido)
  const mostrarAlocacao = entregadoresAtivos !== undefined && pedido.status === 'Pronto'
  // Cancelar é do dono e vale em qualquer status não terminal — pedido já
  // concluído ou cancelado não volta atrás.
  const podeCancelar =
    onCancelar !== undefined && pedido.status !== 'Concluido' && pedido.status !== 'Cancelado'

  async function confirmarCancelamento() {
    if (!onCancelar || motivoCancelamento.trim().length === 0) return

    setEnviandoCancelamento(true)
    const erro = await onCancelar(pedido, motivoCancelamento.trim())
    setEnviandoCancelamento(false)

    if (erro) {
      setErroCancelamento(erro)
      return
    }

    // Sucesso: o pedido sai do quadro no próximo recarregar. Limpa o estado
    // local caso a instância do cartão seja reaproveitada.
    setCancelamentoAberto(false)
    setMotivoCancelamento('')
    setErroCancelamento(null)
  }

  function abrirCancelamento() {
    setErroCancelamento(null)
    setCancelamentoAberto(true)
  }

  function fecharCancelamento() {
    if (enviandoCancelamento) return
    setCancelamentoAberto(false)
    setErroCancelamento(null)
  }
  // Seleção de lote só faz sentido em pedido Pronto (é de lá que se despacha).
  const podeSelecionarLote = onAlternarLote !== undefined && pedido.status === 'Pronto'
  // Mapa visível quando há posição deste pedido e ele está na janela de rastreio.
  const posicaoDoPedido = posicoes.find((p) => p.pedidoId === pedido.id) ?? null
  const mostrarMapa =
    posicaoDoPedido != null && (pedido.status === 'EmRota' || pedido.status === 'Chegou')

  return (
    <Cartao
      ref={setNodeRef}
      style={{ transform: CSS.Translate.toString(transform), transition }}
      className={cn(isDragging && 'opacity-40', className)}
    >
      {/* O arraste vive no corpo, não no cartão inteiro: com os listeners no
          elemento externo, o sensor engolia o clique do botão do rodapé — e o
          cartão ainda ficava com role="button" envolvendo um botão de verdade. */}
      <CartaoCorpo
        {...attributes}
        {...listeners}
        className={cn('flex flex-col gap-2.5', arrastavel && 'cursor-grab touch-none active:cursor-grabbing')}
      >
        {podeSelecionarLote && (
          // Fora da faixa "resumo" de propósito: clicar aqui seleciona, não
          // expande. onPointerDown corta o sensor de arraste do dnd-kit.
          <label
            className="flex items-center gap-2 text-apoio font-medium text-texto-suave"
            onPointerDown={(e) => e.stopPropagation()}
            onClick={(e) => e.stopPropagation()}
          >
            <input
              type="checkbox"
              checked={selecionadoNoLote}
              onChange={() => onAlternarLote?.(pedido)}
              className="size-4 accent-marca-600"
            />
            Incluir no lote
          </label>
        )}

        <div className="flex items-center justify-between gap-3 px-1 pb-3">
          <div className="flex min-w-0 items-center gap-1.5 text-titulo font-bold leading-6 tracking-[-0.01em]">
            <span className={COR_NUMERO[pedido.status]}>#{pedido.numeroExibicao}</span>
            <span className="text-texto-fraco font-normal">·</span>
            {pedido.origem === 'IFood' ? (
              <span className="text-origem-ifood">IFOOD</span>
            ) : (
              <span className="text-texto-suave">{pedido.origem === 'NoventaENove' ? '99 FOOD' : 'INTERNO'}</span>
            )}
          </div>
          <button
            type="button"
            aria-expanded={expandido}
            aria-label={expandido ? 'Recolher pedido' : 'Expandir pedido'}
            onClick={() => setExpandido((v) => !v)}
            className="flex size-6 shrink-0 items-center justify-center rounded-controle text-texto-fraco hover:bg-superficie-alt hover:text-texto"
          >
            <IconeChevron className={cn('size-[15px] transition-transform', !expandido && 'rotate-180')} />
          </button>
        </div>

        {expandido && (
          <div className="flex flex-col gap-2.5">
            <div className="rounded-controle border border-borda bg-superficie-afundada p-2.5">
              <div className="mb-2 flex flex-col gap-0.5 border-b border-borda pb-2">
                <span className="text-rotulo uppercase text-texto-fraco">Cliente</span>
                <strong className="truncate text-apoio font-semibold text-texto">{pedido.clienteNome}</strong>
              </div>
              <p className="mb-2 text-rotulo uppercase text-texto-fraco">Itens</p>
              <ul className="flex flex-col gap-2">
                {pedido.itens.map((item) => (
                  <li key={item.indice} className="flex justify-between gap-3 text-apoio text-texto">
                    <span className="min-w-0">
                      <span className="font-semibold">{item.quantidade}× {item.nome}</span>
                      {item.observacoes && (
                        <span className="block truncate text-texto-suave">{item.observacoes}</span>
                      )}
                    </span>
                    <span className="shrink-0 text-texto-suave tabular-nums">
                      {formatarDinheiro(item.precoTotal)}
                    </span>
                  </li>
                ))}
              </ul>

              <div className="mt-2 flex items-center justify-between gap-2 border-t border-borda pt-2">
                <span className="text-apoio font-medium text-texto">
                  {pedido.itens.length} {pedido.itens.length === 1 ? 'item' : 'itens'}
                </span>
                {precisaCobrar && <Etiqueta tom="alerta">Cobrar</Etiqueta>}
              </div>

              <div className="mt-2 flex items-center justify-between gap-2 text-apoio text-texto-suave">
                <span>{pedido.pagamentoDescricao}</span>
                {precisaCobrar && (
                  <span className="font-semibold text-alerta tabular-nums">
                    receber {formatarDinheiro(pedido.pagamentoValorACobrar)}
                  </span>
                )}
              </div>

              <div className="mt-3 flex items-center justify-between border-t border-borda pt-2">
                <span className="text-apoio font-semibold text-texto">Total do pedido</span>
                <span className={cn('text-titulo font-bold tabular-nums', COR_NUMERO[pedido.status])}>
                  {formatarDinheiro(pedido.valorTotal)}
                </span>
              </div>
            </div>

            {pedido.enderecoResumido && (
              <p className="flex items-center gap-2 rounded-controle border border-borda bg-superficie px-2.5 py-2 text-apoio text-texto-suave">
                <IconeLocal className="size-4 shrink-0 text-texto-fraco" />
                <span className="truncate">{pedido.enderecoResumido}</span>
              </p>
            )}

            <div className="flex flex-wrap items-center gap-2 text-apoio text-texto-suave">
              {pedido.entregadorNome && <span>Entrega · {pedido.entregadorNome}</span>}
              {pedido.ehTeste && <Etiqueta tom="alerta">Pedido de teste</Etiqueta>}
            </div>
          </div>
        )}
      </CartaoCorpo>

      {/* Mapa inline: aparece apenas enquanto o motoboy está em rota ou chegou
          e há posição sendo transmitida via SignalR para este pedido. */}
      {mostrarMapa && (
        <MapaEntrega
          posicoes={posicaoDoPedido ? [posicaoDoPedido] : []}
          className="h-44 w-full rounded-b-controle"
        />
      )}

      {cancelamentoAberto ? (
        // Formulário cobre o rodapé normal enquanto o dono digita o motivo —
        // o botão principal de ação some pra não competir com o "Confirmar
        // cancelamento".
        <CartaoRodape className="flex-col items-stretch gap-2">
          <label className="text-apoio font-medium text-texto-suave" htmlFor={`motivo-${pedido.id}`}>
            Motivo do cancelamento
          </label>
          <input
            id={`motivo-${pedido.id}`}
            type="text"
            value={motivoCancelamento}
            onChange={(e) => {
              setMotivoCancelamento(e.target.value)
              setErroCancelamento(null)
            }}
            placeholder="Ex.: item em falta, loja fechando"
            maxLength={500}
            disabled={enviandoCancelamento}
            autoFocus
            className="rounded-controle border border-borda-forte bg-superficie px-3 py-2 text-apoio text-texto outline-offset-2 placeholder:text-texto-mudo focus-visible:outline-2 focus-visible:outline-marca-600 disabled:opacity-50"
          />
          {erroCancelamento && (
            <p className="text-apoio font-medium text-perigo">{erroCancelamento}</p>
          )}
          <div className="flex gap-2">
            <Botao
              variante="perigo"
              tamanho="pequeno"
              className="flex-1"
              disabled={motivoCancelamento.trim().length === 0}
              carregando={enviandoCancelamento}
              onClick={confirmarCancelamento}
            >
              Confirmar cancelamento
            </Botao>
            <Botao
              variante="secundario"
              tamanho="pequeno"
              disabled={enviandoCancelamento}
              onClick={fecharCancelamento}
            >
              Voltar
            </Botao>
          </div>
        </CartaoRodape>
      ) : mostrarAlocacao ? (
        seletorAberto ? (
          <CartaoRodape className="flex-col items-stretch">
            {entregadoresAtivos && entregadoresAtivos.length > 0 ? (
              entregadoresAtivos.map((entregador) => (
                <Botao
                  key={entregador.courierId}
                  variante="sutil"
                  larguraTotal
                  onClick={() => {
                    onAlocar?.(pedido, entregador.courierId)
                    setSeletorAberto(false)
                  }}
                >
                  {entregador.nome}
                </Botao>
              ))
            ) : (
              <p className="p-2 text-center text-apoio text-texto-fraco">Nenhum motoboy ativo.</p>
            )}
            <Botao variante="secundario" larguraTotal onClick={() => setSeletorAberto(false)}>
              Cancelar
            </Botao>
          </CartaoRodape>
        ) : (
          <CartaoRodape className="flex-col items-stretch">
            <Botao variante="secundario" larguraTotal onClick={() => setSeletorAberto(true)}>
              Alocar Motoboy
            </Botao>
            <div className="flex gap-2">
              <Botao
                variante="acaoDespachar"
                className="flex-1"
                disabled={!pedido.entregadorId}
                onClick={() => onAvancar(pedido)}
              >
                Despachar
              </Botao>
              {podeCancelar && (
                <Botao variante="secundario" className="text-perigo" onClick={abrirCancelamento}>
                  Cancelar
                </Botao>
              )}
            </div>
          </CartaoRodape>
        )
      ) : (
        <CartaoRodape className="flex-col items-stretch">
          {/* Navegação antes da ação: o motoboy precisa sair dirigindo, e só
              volta ao app para marcar o passo seguinte. */}
          {mostrarNavegacao && <BotoesDeNavegacao pedido={pedido} />}

          <div className="flex items-center gap-2">
            <ChipTempoDecorrido desde={pedido.recebidoEm} agora={agora} />
            {mostrarEntregador && pedido.entregadorId && (
              <span className="inline-flex min-w-0 items-center gap-1.5 truncate rounded-controle border border-marca-300 bg-marca-50 px-2 py-1.5 text-apoio font-semibold text-marca-700">
                <span aria-hidden>♙</span>
                <span className="truncate">
                  {pedido.entregadorNome ?? `Entrega #${pedido.entregadorId.slice(0, 4)}`}
                </span>
              </span>
            )}
            {passo && (
              <Botao
                variante={VARIANTE_ACAO[passo.destino] ?? 'primario'}
                tamanho="pequeno"
                className="min-w-0 flex-1"
                onClick={() => onAvancar(pedido)}
              >
                {passo.rotulo}
              </Botao>
            )}
            {/* Cancelar fica no mesmo trilho do botão de ação principal: é
                ação do dono, destrutiva, e precisa estar ao alcance sem abrir
                menu. Sem botão de ação (status onde quem avança é o motoboy),
                aparece sozinho na linha — ainda é a única mão do dono aqui. */}
            {podeCancelar && (
              <Botao
                variante="secundario"
                tamanho="pequeno"
                className="shrink-0 text-perigo"
                onClick={abrirCancelamento}
              >
                Cancelar
              </Botao>
            )}
          </div>
        </CartaoRodape>
      )}
    </Cartao>
  )
}
