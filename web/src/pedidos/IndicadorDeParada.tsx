import { cn } from '@/lib/cn'

interface IndicadorDeParadaProps {
  ordem: number
  estado: 'concluida' | 'atual' | 'pendente'
  /** Esconder o segmento inferior (bolinha é a última parada da corrida). */
  ultima: boolean
}

// Stepper vertical ao lado esquerdo de cada card da lista de entregas do
// motoboy. Inspirado no VerticalLinearStepper do MUI, reimplementado nativo
// (CLAUDE.md §10: não instalar pacote sem necessidade — já temos a estética
// do sistema inteira, trazer MUI só por este indicador seria dois sistemas de
// tema vivendo lado a lado).
//
// Três estados: a parada atual destaca em cor cheia da marca; as concluídas
// mostram o tique; as pendentes ficam só com o número, sem destaque. O
// segmento vertical conecta uma bolinha à próxima — a cor dele reflete o
// progresso (concluído = cor forte, próximas = cinza).
export function IndicadorDeParada({ ordem, estado, ultima }: IndicadorDeParadaProps) {
  const bolinha =
    estado === 'concluida'
      ? 'bg-sucesso text-texto-invertido border-sucesso'
      : estado === 'atual'
        ? 'bg-marca-600 text-white border-marca-600 shadow-[0_0_0_4px_rgba(79,70,229,0.18)]'
        : 'bg-superficie text-texto-fraco border-borda-forte'

  // Segmento inferior: só colore se a parada corrente já estiver concluída —
  // visualmente, a "linha de progresso" só avança quando a entrega foi feita.
  const segmento = estado === 'concluida' ? 'bg-sucesso' : 'bg-borda-forte'

  return (
    <div className="flex shrink-0 flex-col items-center" aria-hidden>
      <span
        className={cn(
          'flex size-7 items-center justify-center rounded-full border-2 text-apoio font-semibold transition-colors',
          bolinha,
        )}
      >
        {estado === 'concluida' ? (
          <svg viewBox="0 0 20 20" className="size-4" fill="currentColor">
            <path d="M7.5 13.5 4 10l1.4-1.4L7.5 10.7l7.1-7.1L16 5z" />
          </svg>
        ) : (
          ordem
        )}
      </span>

      {!ultima && <span className={cn('mt-1 w-0.5 flex-1 transition-colors', segmento)} />}
    </div>
  )
}
