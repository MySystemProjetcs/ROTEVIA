import { useSessao } from '@/auth/SessaoProvider'
import { formatarDinheiro } from '@/lib/tempo'
import { useResumoDashboard } from './useResumoDashboard'

// Faixa compacta de KPIs. Duas medidas em vez de uma pilha: quantidade de
// pedidos e receita do dia — antes empilhados no mesmo card, agora um por
// coluna, alinhados à direita da tela. Deixa o corpo do painel respirar sem
// abrir mão do resumo do dia.
const ESTILO_CARTAO: React.CSSProperties = {
  background: 'linear-gradient(180deg, rgba(255,255,255,0.05), rgba(255,255,255,0.02))',
  border: '1px solid rgba(255,255,255,0.06)',
}

// Cartão magrinho: rótulo em cima, número grande embaixo. Padding menor e
// fonte menor que o card antigo (30px → 20px) — a proporção agora é de
// dado-a-mais no header, não de bloco principal da tela.
function CartaoKpi({
  rotulo,
  valor,
  destaque,
}: {
  rotulo: string
  valor: string
  destaque?: string
}) {
  return (
    <div className="rounded-[10px] px-3 py-2" style={ESTILO_CARTAO}>
      <div className="font-mono text-[10px] uppercase tracking-[0.1em] text-texto-fraco">
        {rotulo}
      </div>
      <div className="mt-0.5 flex items-baseline gap-1.5">
        <span className="text-[20px] font-bold leading-none tracking-tight text-texto tabular-nums">
          {valor}
        </span>
        {destaque && <span className="text-[11px] font-semibold text-alerta">{destaque}</span>}
      </div>
    </div>
  )
}

export function FaixaResumo() {
  const { usuario } = useSessao()
  const merchantId = usuario?.merchantId ?? null
  const { resumo, erro } = useResumoDashboard(merchantId)

  return (
    <div className="flex flex-col gap-2">
      {erro && <p className="text-[12px] text-perigo">{erro}</p>}

      {/* Alinhado à direita, não ocupa a linha inteira: são só dois números,
          não é a atração principal da tela. `ml-auto` empurra pra direita
          dentro do flex do PainelOperacao; `flex-wrap` deixa cair pra baixo
          em tela estreita, evitando quebrar o container pai. */}
      <div className="ml-auto flex flex-wrap items-stretch justify-end gap-2">
        <CartaoKpi
          rotulo="Pedidos hoje"
          valor={resumo?.qtdPedidosHoje?.toString() ?? '—'}
          destaque={resumo?.qtdDeTeste ? `${resumo.qtdDeTeste} teste` : undefined}
        />
        <CartaoKpi
          rotulo="Vendas hoje"
          valor={resumo ? formatarDinheiro(resumo.receitaHoje) : '—'}
        />
      </div>
    </div>
  )
}
