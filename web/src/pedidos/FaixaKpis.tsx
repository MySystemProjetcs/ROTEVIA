import type { Pedido } from '@/dominio/pedido'
import { formatarDinheiro } from '@/lib/tempo'

// 4 indicadores do topo da Central: tudo derivado do array de pedidos do dia
// que já veio do backend — nenhuma chamada extra, nenhum estado. Em andamento
// conta as 6 etapas vivas (Recebido → Chegou/Cobrar), que é o que o lojista
// vê como "pedido pra cuidar agora". Concluído e Cancelado ficam fora.
const ETAPAS_ATIVAS = ['Recebido', 'Confirmado', 'EmPreparo', 'Pronto', 'Despachado', 'Aceito', 'EmRota', 'Chegou', 'Cobrar'] as const
const ETAPAS_ATIVAS_LABEL = 6

interface FaixaKpisProps {
  pedidos: Pedido[]
}

export function FaixaKpis({ pedidos }: FaixaKpisProps) {
  const total = pedidos.length
  const emAndamento = pedidos.filter((p) => (ETAPAS_ATIVAS as readonly string[]).includes(p.status)).length
  const finalizados = pedidos.filter((p) => p.status === 'Concluido').length
  const vendas = pedidos
    .filter((p) => p.status === 'Concluido')
    .reduce((acc, p) => acc + p.valorTotal, 0)

  return (
    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
      <Kpi titulo="Pedidos hoje" valor={total.toString()} legenda="no total" icone={<IconeNota />} />
      <Kpi titulo="Vendas hoje" valor={formatarDinheiro(vendas)} legenda="" icone={<IconeCarteira />} />
      <Kpi
        titulo="Em andamento"
        valor={emAndamento.toString()}
        legenda={`nas ${ETAPAS_ATIVAS_LABEL} etapas ativas`}
        valorClasse="text-marca-400"
        icone={<IconePulso />}
      />
      <Kpi
        titulo="Finalizados"
        valor={finalizados.toString()}
        legenda="pedidos"
        valorClasse="text-acento-400"
        icone={<IconeCheck />}
      />
    </div>
  )
}

function Kpi({
  titulo,
  valor,
  legenda,
  valorClasse = 'text-texto',
  icone,
}: {
  titulo: string
  valor: string
  legenda: string
  valorClasse?: string
  icone: React.ReactNode
}) {
  return (
    <div className="rounded-cartao border border-borda bg-superficie px-4 py-3.5">
      <div className="flex items-center justify-between gap-2">
        <span className="text-apoio font-medium text-texto-suave">{titulo}</span>
        <span aria-hidden className="flex size-7 items-center justify-center rounded-controle border border-borda bg-superficie-alt text-texto-fraco">
          {icone}
        </span>
      </div>
      <div className="mt-1.5 flex items-baseline gap-2">
        <span className={`text-destaque font-bold tabular-nums ${valorClasse}`}>{valor}</span>
        {legenda && <span className="text-apoio text-texto-fraco">{legenda}</span>}
      </div>
    </div>
  )
}

function IconeNota() {
  return (
    <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="5" y="3" width="14" height="18" rx="2" />
      <path d="M9 7h6M9 11h6M9 15h4" />
    </svg>
  )
}

function IconeCarteira() {
  return (
    <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="3" y="6" width="18" height="13" rx="2" />
      <path d="M3 10h18M16 15h2" />
    </svg>
  )
}

function IconePulso() {
  return (
    <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M3 12h4l2-6 4 12 2-6h6" />
    </svg>
  )
}

function IconeCheck() {
  return (
    <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="9" />
      <path d="M8 12l3 3 5-6" />
    </svg>
  )
}
