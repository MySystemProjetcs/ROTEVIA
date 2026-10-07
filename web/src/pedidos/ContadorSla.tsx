import { IconeRelogio } from '@/components/icones/IconeRelogio'
import { formatarDecorrido } from '@/lib/tempo'

interface TempoDecorridoProps {
  desde: string
  agora: number
  rotulo: string
}

// Chip de tempo decorrido do rodapé do cartão. Só leitura de relance — o
// prazo rígido do iFood continua na barra acima, só na coluna Aguardando.
export function ChipTempoDecorrido({ desde, agora }: { desde: string; agora: number }) {
  return (
    <span className="inline-flex shrink-0 items-center gap-1 rounded-controle border border-borda bg-superficie px-2 py-1.5 font-mono text-apoio text-texto-suave tabular-nums">
      <IconeRelogio className="size-3.5 text-texto-fraco" />
      {formatarDecorrido(agora - new Date(desde).getTime())}
    </span>
  )
}

// Versão com rótulo. No cartão do Kanban o chip compacto acima basta — a
// coluna já diz o estado.
export function TempoDecorrido({ desde, agora, rotulo }: TempoDecorridoProps) {
  const decorridoMs = agora - new Date(desde).getTime()

  return (
    <div className="flex items-baseline justify-between">
      <span className="text-rotulo uppercase text-texto-fraco">{rotulo}</span>
      <span className="font-mono text-apoio text-texto-suave">{formatarDecorrido(decorridoMs)}</span>
    </div>
  )
}
