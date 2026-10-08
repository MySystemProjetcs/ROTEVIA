import { useState } from 'react'
import { IconeChevron } from '@/components/icones/IconeChevron'
import type { Pedido } from '@/dominio/pedido'
import { cn } from '@/lib/cn'
import { formatarDinheiro } from '@/lib/tempo'

interface PaneConcluidosProps {
  pedidos: Pedido[]
  agora: number
}

// Pane à direita do Kanban: lista enxuta dos pedidos já finalizados. Não é
// coluna porque arrastar pra cá não cabe (quem termina o pedido é o motoboy
// no app dele, ou o próprio evento CONCLUDED do iFood). Rola internamente
// pra não esticar a página — mesmo princípio da coluna "Finalizados" antiga.
export function PaneConcluidos({ pedidos, agora }: PaneConcluidosProps) {
  const concluidos = pedidos
    .filter((p) => p.status === 'Concluido')
    .sort((a, b) => (b.recebidoEm > a.recebidoEm ? 1 : -1))

  // Set dos ids expandidos: default vazio = todos recolhidos. Estado por
  // sessão (não persiste): quem abre pra conferir fecha depois.
  const [expandidos, setExpandidos] = useState<Set<string>>(new Set())
  function alternar(id: string) {
    setExpandidos((atual) => {
      const prox = new Set(atual)
      if (prox.has(id)) prox.delete(id)
      else prox.add(id)
      return prox
    })
  }

  return (
    <aside className="flex min-h-0 w-full flex-col gap-3 rounded-cartao border border-borda bg-superficie p-3 lg:w-80 lg:shrink-0">
      <header className="flex items-center justify-between gap-2 border-b border-borda pb-2">
        <div className="flex items-center gap-2">
          <span aria-hidden className="size-2 rounded-full bg-estado-concluido" />
          <h3 className="text-rotulo uppercase tracking-[0.12em] text-texto-fraco">Finalizados</h3>
        </div>
        <span className="font-mono tabular-nums text-apoio text-texto-suave">{concluidos.length}</span>
      </header>

      {concluidos.length === 0 ? (
        <p className="py-6 text-center text-apoio text-texto-fraco">
          Nenhum pedido finalizado hoje ainda.
        </p>
      ) : (
        <ul className="flex flex-col gap-2 overflow-y-auto">
          {concluidos.map((p) => {
            const aberto = expandidos.has(p.id)
            return (
              <li
                key={p.id}
                className="flex flex-col rounded-controle border border-borda bg-superficie-alt"
              >
                {/* Cabeçalho sempre visível: identifica o pedido mesmo
                    recolhido. O botão inteiro é o alvo de clique (toque fácil
                    no celular), com aria-expanded pra leitor de tela. */}
                <button
                  type="button"
                  onClick={() => alternar(p.id)}
                  aria-expanded={aberto}
                  aria-controls={`concluido-${p.id}`}
                  className="flex items-center justify-between gap-2 rounded-controle px-3 py-2.5 text-left text-apoio outline-offset-2 hover:bg-superficie-afundada focus-visible:outline-2 focus-visible:outline-marca-600"
                >
                  <span className="flex items-center gap-2">
                    <span className="font-semibold text-texto">#{p.numeroExibicao}</span>
                    <ChipOrigem origem={p.origem} />
                  </span>
                  <IconeChevron
                    className={cn(
                      'size-4 shrink-0 text-texto-fraco transition-transform',
                      aberto && 'rotate-180',
                    )}
                  />
                </button>

                {aberto && (
                  <dl
                    id={`concluido-${p.id}`}
                    className="flex flex-col gap-2 border-t border-borda px-3 py-2.5 text-apoio"
                  >
                    <LinhaDetalhe rotulo="Cliente" valor={p.clienteNome || 'Pedido de teste'} />
                    {p.enderecoResumido && (
                      <LinhaDetalhe rotulo="Endereço" valor={p.enderecoResumido} />
                    )}
                    <LinhaDetalhe
                      rotulo="Entregue por"
                      valor={p.entregadorNome ?? 'Sem motoboy registrado'}
                      destaque={!!p.entregadorNome}
                    />
                    <LinhaDetalhe
                      rotulo="Itens"
                      valor={`${p.itens.length} ${p.itens.length === 1 ? 'item' : 'itens'}`}
                    />
                    <LinhaDetalhe rotulo="Pagamento" valor={p.pagamentoDescricao} />
                    {p.pagamentoValorACobrar > 0 && (
                      <LinhaDetalhe
                        rotulo="Cobrado na entrega"
                        valor={formatarDinheiro(p.pagamentoValorACobrar)}
                      />
                    )}
                    <LinhaDetalhe
                      rotulo="Total do pedido"
                      valor={formatarDinheiro(p.valorTotal)}
                      destaque
                    />
                    <LinhaDetalhe
                      rotulo="Tempo total"
                      valor={tempoDesde(p.recebidoEm, agora)}
                    />
                    {p.codigoDeEntrega && (
                      <LinhaDetalhe
                        rotulo="Código de entrega"
                        valor={p.codigoDeEntrega}
                        mono
                      />
                    )}
                    {p.ehTeste && (
                      <span className="mt-1 inline-flex self-start rounded-controle border border-alerta/40 bg-alerta-fundo px-2 py-0.5 text-rotulo uppercase tracking-wider text-alerta">
                        Pedido de teste
                      </span>
                    )}
                  </dl>
                )}
              </li>
            )
          })}
        </ul>
      )}
    </aside>
  )
}

// Linha rótulo → valor dentro do bloco expandido. `destaque` deixa o valor em
// texto cheio; `mono` usa a fonte mono (código, número). Padrão é apoio
// discreto, pra o pedido inteiro caber sem virar parede.
function LinhaDetalhe({
  rotulo,
  valor,
  destaque = false,
  mono = false,
}: {
  rotulo: string
  valor: string
  destaque?: boolean
  mono?: boolean
}) {
  return (
    <div className="flex items-start justify-between gap-3">
      <dt className="shrink-0 text-rotulo uppercase tracking-wider text-texto-fraco">
        {rotulo}
      </dt>
      <dd
        className={`min-w-0 flex-1 truncate text-right ${
          destaque ? 'font-semibold text-texto' : 'text-texto-suave'
        } ${mono ? 'font-mono tabular-nums' : ''}`}
      >
        {valor}
      </dd>
    </div>
  )
}

function ChipOrigem({ origem }: { origem: Pedido['origem'] }) {
  const rotulo = origem === 'IFood' ? 'iFood' : origem === 'NoventaENove' ? '99 Food' : 'Interno'
  const cor = origem === 'IFood' ? 'text-origem-ifood' : 'text-texto-suave'
  return <span className={`text-rotulo uppercase tracking-wider ${cor}`}>{rotulo}</span>
}

function IconeRelogio() {
  return (
    <svg viewBox="0 0 24 24" width="12" height="12" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="9" />
      <path d="M12 7v5l3 2" />
    </svg>
  )
}

function tempoDesde(iso: string, agora: number): string {
  const minutos = Math.max(0, Math.round((agora - new Date(iso).getTime()) / 60000))
  if (minutos < 60) return `${minutos} min`
  const horas = Math.floor(minutos / 60)
  const resto = minutos % 60
  return resto === 0 ? `${horas}h` : `${horas}h${resto.toString().padStart(2, '0')}`
}
