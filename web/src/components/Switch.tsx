import { useId } from 'react'
import type { ReactNode } from 'react'
import { cn } from '@/lib/cn'

interface SwitchProps {
  marcado: boolean
  onMudar: (marcado: boolean) => void
  rotulo: string
  /** Aparece dentro da trilha quando ligado. */
  indicador?: ReactNode
  /** Aparece dentro da trilha quando desligado. */
  indicadorDesligado?: ReactNode
  /** Cor da trilha ligada — token do tema, nunca cor crua. */
  classeLigado?: string
}

// Checkbox de verdade por baixo, escondido: entrega teclado (espaço alterna),
// leitor de tela e envio em formulário sem reimplementar nada. O visual é a
// trilha desenhada em volta dele.
export function Switch({
  marcado,
  onMudar,
  rotulo,
  indicador,
  indicadorDesligado,
  classeLigado = 'bg-sucesso',
}: SwitchProps) {
  const id = useId()

  return (
    <label htmlFor={id} className="inline-flex cursor-pointer items-center gap-2.5">
      <input
        id={id}
        type="checkbox"
        role="switch"
        checked={marcado}
        onChange={(e) => onMudar(e.target.checked)}
        className="peer sr-only"
      />

      <span
        aria-hidden
        className={cn(
          'relative inline-flex h-6 w-11 shrink-0 items-center rounded-controle p-0.5 transition-colors',
          // O foco vem do input escondido, via peer: sem isto quem navega por
          // teclado não veria onde está.
          'peer-focus-visible:outline-2 peer-focus-visible:outline-offset-2 peer-focus-visible:outline-marca-600',
          marcado ? classeLigado : 'bg-borda-forte',
        )}
      >
        <span
          className={cn(
            'inline-flex size-5 items-center justify-center rounded-controle bg-superficie shadow-cartao transition-transform',
            marcado ? 'translate-x-5' : 'translate-x-0',
          )}
        >
          {marcado ? indicador : indicadorDesligado}
        </span>
      </span>

      <span className="text-corpo text-texto">{rotulo}</span>
    </label>
  )
}
