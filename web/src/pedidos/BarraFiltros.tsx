import { IconeBusca } from '@/components/icones/IconeBusca'

export type PeriodoFiltro = 'hoje' | 'ontem' | 'semana'
export type CanalFiltro = 'todos' | 'IFood' | 'NoventaENove' | 'Interno'
export type VisaoKanban = 'kanban' | 'lista'

interface BarraFiltrosProps {
  busca: string
  onBusca: (v: string) => void
  periodo: PeriodoFiltro
  onPeriodo: (v: PeriodoFiltro) => void
  canal: CanalFiltro
  onCanal: (v: CanalFiltro) => void
  visao: VisaoKanban
  onVisao: (v: VisaoKanban) => void
}

// Barra de filtros do topo do quadro: busca, janela de tempo, canal de origem
// e visão (Kanban/Lista). Os selects são nativos de propósito — menor dívida
// de UI até existir decisão explícita por combobox custom.
export function BarraFiltros({
  busca,
  onBusca,
  periodo,
  onPeriodo,
  canal,
  onCanal,
  visao,
  onVisao,
}: BarraFiltrosProps) {
  return (
    <div className="flex flex-wrap items-center gap-2">
      <label className="flex min-w-0 flex-1 items-center gap-2 rounded-controle border border-borda bg-superficie-alt px-3 py-2 text-texto-fraco sm:min-w-[280px] sm:flex-none sm:basis-80">
        <IconeBusca className="size-3.5 shrink-0" />
        <input
          value={busca}
          onChange={(e) => onBusca(e.target.value)}
          placeholder="Buscar por pedido ou código de entrega"
          className="w-full bg-transparent text-apoio text-texto placeholder:text-texto-mudo outline-none"
        />
      </label>

      <Select
        rotulo="Período"
        valor={periodo}
        onChange={(v) => onPeriodo(v as PeriodoFiltro)}
        opcoes={[
          { v: 'hoje', t: 'Hoje' },
          { v: 'ontem', t: 'Ontem' },
          { v: 'semana', t: 'Esta semana' },
        ]}
      />

      <Select
        rotulo="Canal"
        valor={canal}
        onChange={(v) => onCanal(v as CanalFiltro)}
        opcoes={[
          { v: 'todos', t: 'Todos os canais' },
          { v: 'IFood', t: 'iFood' },
          { v: 'NoventaENove', t: '99 Food' },
          { v: 'Interno', t: 'Interno' },
        ]}
      />

      <div className="ml-auto inline-flex rounded-controle border border-borda bg-superficie-alt p-0.5 text-apoio text-texto-fraco">
        <button
          type="button"
          aria-pressed={visao === 'kanban'}
          onClick={() => onVisao('kanban')}
          className={`flex items-center gap-1.5 rounded-[6px] px-2.5 py-1.5 ${
            visao === 'kanban' ? 'bg-marca-600 text-white' : 'hover:text-texto'
          }`}
        >
          <IconeKanban /> Kanban
        </button>
        <button
          type="button"
          aria-pressed={visao === 'lista'}
          onClick={() => onVisao('lista')}
          className={`flex items-center gap-1.5 rounded-[6px] px-2.5 py-1.5 ${
            visao === 'lista' ? 'bg-marca-600 text-white' : 'hover:text-texto'
          }`}
          aria-label="Visão em lista"
        >
          <IconeLista />
        </button>
      </div>
    </div>
  )
}

function Select({
  rotulo,
  valor,
  onChange,
  opcoes,
}: {
  rotulo: string
  valor: string
  onChange: (v: string) => void
  opcoes: { v: string; t: string }[]
}) {
  return (
    <label className="inline-flex items-center gap-2 rounded-controle border border-borda bg-superficie-alt px-3 py-2 text-apoio text-texto-fraco">
      <span>{rotulo}</span>
      <select
        value={valor}
        onChange={(e) => onChange(e.target.value)}
        className="bg-transparent font-semibold text-texto outline-none"
      >
        {opcoes.map((o) => (
          <option key={o.v} value={o.v} className="bg-superficie text-texto">
            {o.t}
          </option>
        ))}
      </select>
    </label>
  )
}

function IconeKanban() {
  return (
    <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="4" y="4" width="5" height="16" rx="1" />
      <rect x="11" y="4" width="5" height="10" rx="1" />
      <rect x="18" y="4" width="2" height="14" rx="1" />
    </svg>
  )
}

function IconeLista() {
  return (
    <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M5 7h14M5 12h14M5 17h14" />
    </svg>
  )
}
