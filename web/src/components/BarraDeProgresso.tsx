import { cn } from '@/lib/cn'

type TomBarra = 'neutro' | 'marca' | 'alerta' | 'perigo' | 'sucesso'

const TRILHOS: Record<TomBarra, string> = {
  neutro: 'bg-superficie-afundada',
  marca: 'bg-marca-100',
  alerta: 'bg-alerta-fundo',
  perigo: 'bg-perigo-fundo',
  sucesso: 'bg-sucesso-fundo',
}

const PREENCHIMENTOS: Record<TomBarra, string> = {
  neutro: 'bg-texto-fraco',
  marca: 'bg-marca-600',
  alerta: 'bg-alerta',
  perigo: 'bg-perigo',
  sucesso: 'bg-sucesso',
}

interface BarraDeProgressoProps {
  /** 0 a 1. */
  progresso: number
  tom?: TomBarra
  rotuloAcessivel: string
}

export function BarraDeProgresso({ progresso, tom = 'marca', rotuloAcessivel }: BarraDeProgressoProps) {
  const percentual = Math.round(Math.min(Math.max(progresso, 0), 1) * 100)

  return (
    <div
      role="progressbar"
      aria-valuenow={percentual}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-label={rotuloAcessivel}
      className={cn('h-1.5 w-full overflow-hidden rounded-controle', TRILHOS[tom])}
    >
      <div
        className={cn('h-full rounded-controle transition-[width] duration-1000 ease-linear', PREENCHIMENTOS[tom])}
        style={{ width: `${percentual}%` }}
      />
    </div>
  )
}
