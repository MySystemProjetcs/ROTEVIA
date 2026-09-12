import { BarraDeProgresso } from '@/components/BarraDeProgresso'
import { formatarDecorrido, formatarDuracao } from '@/lib/tempo'

// Janela total de confirmação do iFood. Serve só para desenhar a barra — o
// instante do prazo vem pronto do servidor, nunca é calculado aqui.
const JANELA_MS = 8 * 60 * 1000

interface ContadorSlaProps {
  prazoAte: string
  agora: number
}

// Barra que esvazia até o prazo de confirmação. O pedido não é movido quando
// ela zera: quem cancela é o iFood, e só quando o evento CANCELLED chegar pelo
// polling é que o cartão muda de coluna. Mover antes seria a tela mentir nos
// casos em que a confirmação em cima do prazo ainda é aceita.
export function ContadorSla({ prazoAte, agora }: ContadorSlaProps) {
  const restanteMs = new Date(prazoAte).getTime() - agora
  const expirado = restanteMs <= 0

  const progresso = Math.min(Math.max(restanteMs / JANELA_MS, 0), 1)
  const tom = expirado ? 'perigo' : progresso > 0.5 ? 'marca' : progresso > 0.2 ? 'alerta' : 'perigo'

  return (
    <div className="flex flex-col gap-1">
      <div className="flex items-baseline justify-between">
        <span className="text-rotulo uppercase text-texto-fraco">
          {expirado ? 'Prazo esgotado' : 'Confirmar em'}
        </span>
        <span
          className={
            expirado ? 'font-mono text-apoio text-perigo' : 'font-mono text-apoio font-medium text-texto'
          }
        >
          {expirado ? `há ${formatarDecorrido(-restanteMs)}` : formatarDuracao(restanteMs)}
        </span>
      </div>

      <BarraDeProgresso
        progresso={progresso}
        tom={tom}
        rotuloAcessivel="Tempo restante para confirmar o pedido"
      />

      {expirado && (
        <span className="text-apoio text-perigo">
          O iFood pode cancelar a qualquer momento.
        </span>
      )}
    </div>
  )
}

interface TempoDecorridoProps {
  desde: string
  agora: number
  rotulo: string
}

// Para os estados após a confirmação não existe prazo rígido do iFood, então
// mostramos há quanto tempo o pedido está parado — é o que revela o pedido
// esquecido na cozinha.
export function TempoDecorrido({ desde, agora, rotulo }: TempoDecorridoProps) {
  const decorridoMs = agora - new Date(desde).getTime()

  return (
    <div className="flex items-baseline justify-between">
      <span className="text-rotulo uppercase text-texto-fraco">{rotulo}</span>
      <span className="font-mono text-apoio text-texto-suave">{formatarDecorrido(decorridoMs)}</span>
    </div>
  )
}
