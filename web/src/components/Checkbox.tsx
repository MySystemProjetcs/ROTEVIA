import { useId } from 'react'
import { cn } from '@/lib/cn'

interface CheckboxProps {
  marcado: boolean
  onMudar: (marcado: boolean) => void
  rotulo: string
  disabled?: boolean
}

// Modelo do MUI Checkbox (https://mui.com/material-ui/react-checkbox/):
// quadrado com contorno quando desmarcado, preenchido com check quando
// marcado, e um halo translúcido atrás do ícone que aparece no hover/foco —
// o "ripple" parado do Material Design. Checkbox de verdade por baixo,
// escondido (mesmo padrão do Switch): teclado, leitor de tela e formulário
// funcionam sem reimplementar nada; o visual é só o ícone + halo por cima.
export function Checkbox({ marcado, onMudar, rotulo, disabled = false }: CheckboxProps) {
  const id = useId()

  return (
    <label
      htmlFor={id}
      className={cn(
        'group inline-flex items-center gap-2',
        disabled ? 'cursor-not-allowed opacity-50' : 'cursor-pointer',
      )}
    >
      <span className="relative inline-flex size-10 shrink-0 items-center justify-center">
        <input
          id={id}
          type="checkbox"
          checked={marcado}
          disabled={disabled}
          onChange={(e) => onMudar(e.target.checked)}
          className="peer sr-only"
        />

        {/* Halo translúcido: transparente em repouso, ganha opacidade no hover
            e no foco por teclado — nunca um preenchimento sólido. */}
        <span
          aria-hidden
          className={cn(
            'absolute inset-0 rounded-full bg-marca-600 opacity-0 transition-opacity',
            'group-hover:opacity-10',
            'peer-focus-visible:opacity-15',
          )}
        />

        <svg
          aria-hidden
          viewBox="0 0 24 24"
          className={cn(
            'relative size-6 transition-colors',
            marcado ? 'text-marca-600' : 'text-texto-fraco',
          )}
        >
          {marcado ? (
            <>
              <rect x="3" y="3" width="18" height="18" rx="3" fill="currentColor" />
              <path
                d="M9 16.2 4.8 12l-1.4 1.4L9 19l11-11-1.4-1.4z"
                fill="var(--color-texto-invertido)"
              />
            </>
          ) : (
            <rect
              x="3.5"
              y="3.5"
              width="17"
              height="17"
              rx="2.5"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
            />
          )}
        </svg>
      </span>

      <span className="text-corpo text-texto">{rotulo}</span>
    </label>
  )
}
