import { useSortable } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { useState } from 'react'
import { Botao } from '@/components/Botao'
import { Cartao, CartaoCorpo, CartaoRodape } from '@/components/Cartao'
import { Etiqueta, EtiquetaEstado } from '@/components/Etiqueta'
import { IconeLocal } from '@/components/icones/IconeLocal'
import type { ObterProximoPasso, Pedido, StatusPedido } from '@/dominio/pedido'
import { proximoPasso, temValorACobrar } from '@/dominio/pedido'
import type { Entregador } from '@/dominio/entregador'
import { cn } from '@/lib/cn'
import { formatarDinheiro } from '@/lib/tempo'
import { BotoesDeNavegacao } from './BotoesDeNavegacao'
import { ChipTempoDecorrido, ContadorSla } from './ContadorSla'
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
  trilhaRastreio?: PosicaoEntregador[]
}

export function CartaoPedido({
  pedido,
  agora,
  onAvancar,
  arrastavel = true,
  mostrarEstado = false,
  className,
  obterProximoPasso = proximoPasso,
  entregadoresAtivos,
  onAlocar,
  posicoes = [],
  trilhaRastreio = [],
}: CartaoPedidoProps) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: pedido.id,
    disabled: !arrastavel,
  })
  const [seletorAberto, setSeletorAberto] = useState(false)

  const passo = obterProximoPasso(pedido)
  const precisaCobrar = temValorACobrar(pedido)
  const mostrarAlocacao = entregadoresAtivos !== undefined && pedido.status === 'Pronto'
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
        {/* Só vem preenchido na listagem do motoboy — ele pode atender
            mais de uma loja, então precisa saber de qual pedido é esse. */}
        {pedido.nomeLoja && <p className="text-apoio font-medium text-marca-600">{pedido.nomeLoja}</p>}

        <div className="flex items-center justify-between gap-2">
          <span className={cn('text-titulo', COR_NUMERO[pedido.status])}>#{pedido.numeroExibicao}</span>
          {mostrarEstado && <EtiquetaEstado estado={pedido.status} />}
        </div>
        <p className="-mt-2 text-corpo font-semibold text-texto">{pedido.clienteNome}</p>

        {pedido.status === 'Recebido' && <ContadorSla prazoAte={pedido.prazoConfirmacaoAte} agora={agora} />}

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
            <span className="flex items-center gap-1.5">
              {/* Ao lado do valor, não no rodapé: é aqui que o olho vai quando
                  a pergunta é "recebo quanto, e de quem". */}
              {precisaCobrar && <Etiqueta tom="alerta">Cobrar</Etiqueta>}
              <span className={cn('text-corpo font-semibold tabular-nums', COR_NUMERO[pedido.status])}>
                {formatarDinheiro(pedido.valorTotal)}
              </span>
            </span>
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
      </CartaoCorpo>

      {/* Mapa inline: aparece apenas enquanto o motoboy está em rota ou chegou
          e há posição sendo transmitida via SignalR para este pedido. */}
      {mostrarMapa && (
        <MapaEntrega
          posicoes={posicaoDoPedido ? [posicaoDoPedido] : []}
          trilha={trilhaRastreio}
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
          <BotoesDeNavegacao pedido={pedido} />

          <div className="flex items-center gap-2">
            <ChipTempoDecorrido desde={pedido.recebidoEm} agora={agora} />
            {passo && (
              <Botao
                variante={VARIANTE_ACAO[passo.destino] ?? 'primario'}
                className="flex-1"
                onClick={() => onAvancar(pedido)}
              >
                {passo.rotulo}
              </Botao>
            )}
          </div>
        </CartaoRodape>
      )}
    </Cartao>
  )
}
