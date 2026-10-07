import { useSortable } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { useState } from 'react'
import { Botao } from '@/components/Botao'
import { Cartao, CartaoCorpo, CartaoRodape } from '@/components/Cartao'
import { Etiqueta, EtiquetaEstado } from '@/components/Etiqueta'
import { IconeChevron } from '@/components/icones/IconeChevron'
import { IconeLocal } from '@/components/icones/IconeLocal'
import { SeloOrigem } from '@/components/SeloOrigem'
import type { ObterProximoPasso, Pedido, StatusPedido } from '@/dominio/pedido'
import { proximoPasso, temValorACobrar } from '@/dominio/pedido'
import type { Entregador } from '@/dominio/entregador'
import { cn } from '@/lib/cn'
import { formatarDinheiro } from '@/lib/tempo'
import { BotoesDeNavegacao } from './BotoesDeNavegacao'
import { ChipTempoDecorrido } from './ContadorSla'
import { MapaEntrega } from './MapaEntrega'
import type { PosicaoEntregador } from './useRastreio'

// Número do pedido na cor da etapa. Sem etiqueta de estado no cartão: a
// coluna onde ele está já diz isso — repetir seria ruído visual.
const COR_NUMERO: Record<StatusPedido, string> = {
  Recebido: 'text-estado-recebido',
  Confirmado: 'text-estado-confirmado',
  EmPreparo: 'text-estado-preparo',
  Pronto: 'text-estado-pronto',
  Despachado: 'text-estado-despachado',
  Aceito: 'text-estado-aceito',
  EmRota: 'text-estado-emrota',
  Chegou: 'text-estado-chegou',
  Cobrar: 'text-estado-cobrar',
  Concluido: 'text-estado-concluido',
  Cancelado: 'text-estado-cancelado',
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
  // Só o dono recebe isto — cancelar é ação de gestão da loja, disponível em
  // qualquer status não terminal. Devolve mensagem de erro (ou null) para
  // exibir junto ao campo de motivo.
  onCancelar?: (pedido: Pedido, motivo: string) => Promise<string | null>
  // Pedidos casados (só no quadro do dono, em cards Prontos): quando presente,
  // o card mostra uma caixa de seleção para incluir o pedido num despacho em
  // lote. `selecionadoNoLote` reflete o estado da seleção.
  onAlternarLote?: (pedido: Pedido) => void
  selecionadoNoLote?: boolean
}

export function CartaoPedido({
  pedido,
  agora,
  onAvancar,
  arrastavel = true,
  mostrarNavegacao = false,
  mostrarEstado = false,
  className,
  obterProximoPasso = proximoPasso,
  entregadoresAtivos,
  onAlocar,
  posicoes = [],
  onCancelar,
  onAlternarLote,
  selecionadoNoLote = false,
}: CartaoPedidoProps) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: pedido.id,
    disabled: !arrastavel,
  })
  const [seletorAberto, setSeletorAberto] = useState(false)
  // Recolhido por padrão: com a grade cheia, cartão mostrando item a item
  // vira parede de texto. Fica só no cartão (não no pedido), então cada card
  // lembra seu próprio estado entre as atualizações de 4s do polling — o
  // React mantém a mesma instância porque a lista é chaveada por pedido.id.
  const [expandido, setExpandido] = useState(false)
  const [cancelamentoAberto, setCancelamentoAberto] = useState(false)
  const [motivoCancelamento, setMotivoCancelamento] = useState('')
  const [enviandoCancelamento, setEnviandoCancelamento] = useState(false)
  const [erroCancelamento, setErroCancelamento] = useState<string | null>(null)

  const passo = obterProximoPasso(pedido)
  const precisaCobrar = temValorACobrar(pedido)
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
  const mostrarAlocacao = entregadoresAtivos !== undefined && pedido.status === 'Pronto'
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

        {/* Resumo: o único bloco visível com o card recolhido, e o gatilho do
            expandir/recolher. role="button" próprio (não o Cartao inteiro)
            porque o clique não pode competir com o arraste do dnd-kit nem
            com os botões do rodapé — só esta faixa alterna o estado. */}
        <div
          role="button"
          tabIndex={0}
          aria-expanded={expandido}
          onClick={() => setExpandido((v) => !v)}
          onKeyDown={(e) => {
            if (e.key === 'Enter' || e.key === ' ') {
              e.preventDefault()
              setExpandido((v) => !v)
            }
          }}
          className="-m-1 flex cursor-pointer flex-col gap-1 rounded-controle p-1 outline-offset-2 focus-visible:outline-2 focus-visible:outline-marca-600"
        >
          {/* Só vem preenchido na listagem do motoboy — ele pode atender
              mais de uma loja, então precisa saber de qual pedido é esse. */}
          {pedido.nomeLoja && <p className="text-apoio font-medium text-marca-600">{pedido.nomeLoja}</p>}

          <div className="flex items-center justify-between gap-2">
            <span className={cn('text-titulo', COR_NUMERO[pedido.status])}>#{pedido.numeroExibicao}</span>
            {/* Canto superior direito: de onde veio, em que etapa está
                (quando o card não está numa coluna que já diz isso), e o
                indicador de que dá pra abrir. */}
            <span className="flex items-center gap-1.5">
              {pedido.ordemNaRota != null && (
                <span className="rounded-full border border-marca-300 bg-marca-50 px-2 py-0.5 text-apoio font-semibold text-marca-700">
                  Parada {pedido.ordemNaRota}
                </span>
              )}
              <SeloOrigem origem={pedido.origem} />
              {mostrarEstado && <EtiquetaEstado estado={pedido.status} />}
              <IconeChevron
                className={cn('size-4 shrink-0 text-texto-fraco transition-transform', expandido && 'rotate-180')}
              />
            </span>
          </div>
          <p className="-mt-2 flex items-center justify-between gap-2 text-corpo font-semibold text-texto">
            <span className="truncate">{pedido.clienteNome}</span>
            {/* Total sempre visível recolhido: é o dado que mais importa ver
                sem precisar abrir o card. */}
            <span className={cn('shrink-0 tabular-nums', COR_NUMERO[pedido.status])}>
              {formatarDinheiro(pedido.valorTotal)}
            </span>
          </p>
        </div>

        {expandido && (
          <>
            <div className="rounded-controle bg-superficie-afundada p-2.5">
              <ul className="flex flex-col gap-1">
                {pedido.itens.map((item) => (
                  <li key={item.indice} className="flex justify-between gap-2 text-apoio text-texto">
                    <span>
                      <span className="font-medium">{item.quantidade}×</span> {item.nome}
                    </span>
                    <span className="shrink-0 text-texto-suave tabular-nums">
                      {formatarDinheiro(item.precoTotal)}
                    </span>
                  </li>
                ))}
              </ul>

              <div className="mt-1.5 flex items-center justify-between gap-2 border-t border-borda pt-1.5">
                <span className="text-apoio font-medium text-texto">
                  {pedido.itens.length} {pedido.itens.length === 1 ? 'item' : 'itens'}
                </span>
                {precisaCobrar && <Etiqueta tom="alerta">Cobrar</Etiqueta>}
              </div>

              <p className="mt-1.5 flex items-center justify-between gap-2 text-apoio text-texto-suave">
                <span>{pedido.pagamentoDescricao}</span>
                {precisaCobrar && (
                  <span className="font-semibold text-alerta tabular-nums">
                    receber {formatarDinheiro(pedido.pagamentoValorACobrar)}
                  </span>
                )}
              </p>
            </div>

            {/* Nulo quando é retirada no balcão ou consumo no local. */}
            {pedido.enderecoResumido && (
              <p className="flex items-center gap-1.5 rounded-controle border border-borda px-2.5 py-2 text-apoio text-texto-suave">
                <IconeLocal className="size-4 shrink-0 text-texto-fraco" />
                <span className="truncate">{pedido.enderecoResumido}</span>
              </p>
            )}

            {pedido.entregadorNome && (
              <p className="text-apoio text-texto-suave">Motoboy: {pedido.entregadorNome}</p>
            )}

            {pedido.ehTeste && <Etiqueta tom="alerta">Pedido de teste</Etiqueta>}
          </>
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

      {mostrarAlocacao ? (
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
            <Botao
              variante="acaoDespachar"
              larguraTotal
              disabled={!pedido.entregadorId}
              onClick={() => onAvancar(pedido)}
            >
              Despachar
            </Botao>
          </CartaoRodape>
        )
      ) : (
        <CartaoRodape className="flex-col items-stretch">
          {/* Navegação antes da ação: o motoboy precisa sair dirigindo, e só
              volta ao app para marcar o passo seguinte. */}
          {mostrarNavegacao && <BotoesDeNavegacao pedido={pedido} />}

          <div className="flex items-center gap-2">
            <ChipTempoDecorrido desde={pedido.recebidoEm} agora={agora} />
            {passo && (
              <Botao
                variante={VARIANTE_ACAO[passo.destino] ?? 'primario'}
                tamanho="pequeno"
                className="flex-1"
                onClick={() => onAvancar(pedido)}
              >
                {passo.rotulo}
              </Botao>
            )}
          </div>
        </CartaoRodape>
      )}

      {podeCancelar && (
        <div className="border-t border-borda px-4 py-2">
          {!cancelamentoAberto ? (
            <button
              type="button"
              onClick={() => setCancelamentoAberto(true)}
              className="text-apoio font-medium text-perigo underline-offset-2 hover:underline"
            >
              Cancelar pedido
            </button>
          ) : (
            <div className="flex flex-col gap-2">
              <label className="text-apoio text-texto-suave">Motivo do cancelamento</label>
              <input
                type="text"
                value={motivoCancelamento}
                onChange={(e) => {
                  setMotivoCancelamento(e.target.value)
                  setErroCancelamento(null)
                }}
                placeholder="Ex.: item em falta, loja fechando"
                maxLength={500}
                disabled={enviandoCancelamento}
                className="rounded-controle border border-borda-forte px-3 py-2 text-apoio text-texto outline-offset-2 placeholder:text-texto-mudo focus-visible:outline-2 focus-visible:outline-marca-600 disabled:opacity-50"
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
                  onClick={() => {
                    setCancelamentoAberto(false)
                    setErroCancelamento(null)
                  }}
                >
                  Voltar
                </Botao>
              </div>
            </div>
          )}
        </div>
      )}
    </Cartao>
  )
}
