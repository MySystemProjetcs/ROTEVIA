import { useId } from 'react'
import type { InputHTMLAttributes } from 'react'
import { cn } from '@/lib/cn'

interface CampoProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'id'> {
  rotulo: string
  erro?: string
  apoio?: string
}

export function Campo({ rotulo, erro, apoio, className, ...props }: CampoProps) {
  const id = useId()
  const idApoio = `${id}-apoio`

  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-apoio font-medium text-texto">
        {rotulo}
      </label>

      <input
        {...props}
        id={id}
        aria-invalid={erro ? true : undefined}
        aria-describedby={erro || apoio ? idApoio : undefined}
        className={cn(
          'h-toque rounded-controle border bg-superficie px-3 text-corpo text-texto',
          'placeholder:text-texto-fraco',
          'outline-offset-2 focus-visible:outline-2 focus-visible:outline-marca-600',
          'disabled:cursor-not-allowed disabled:bg-superficie-afundada',
          erro ? 'border-perigo' : 'border-borda-forte',
          className,
        )}
      />

      {(erro || apoio) && (
        <span id={idApoio} className={cn('text-apoio', erro ? 'text-perigo' : 'text-texto-suave')}>
          {erro ?? apoio}
        </span>
      )}
    </div>
  )
}
