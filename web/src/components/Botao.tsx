import type { ButtonHTMLAttributes, ReactNode } from 'react'
import { cn } from '@/lib/cn'

type Variante =
  | 'primario'
  | 'secundario'
  | 'sutil'
  | 'perigo'
  | 'acaoConfirmar'
  | 'acaoIniciarPreparo'
  | 'acaoMarcarPronto'
  | 'acaoDespachar'
type Tamanho = 'medio' | 'grande'

// Toda diferença de aparência é uma variante declarada aqui. Precisou de um
// botão diferente? Entra como variante nova — nunca alterando as existentes,
// que já estão em uso em outras telas.
const VARIANTES: Record<Variante, string> = {
  primario: 'bg-marca-600 text-texto-invertido hover:bg-marca-700 active:bg-marca-800',
  secundario: 'bg-superficie text-texto border border-borda-forte hover:bg-superficie-alt',
  sutil: 'bg-transparent text-marca-600 hover:bg-marca-50',
  perigo: 'bg-perigo text-texto-invertido hover:opacity-90',
  // Ações do Kanban na cor da etapa de destino. Texto branco em todas: os tons
  // foram escolhidos escuros de propósito para o contraste.
  acaoConfirmar: 'bg-alerta text-texto-invertido hover:opacity-90',
  acaoIniciarPreparo: 'bg-marca-600 text-texto-invertido hover:bg-marca-700',
  acaoMarcarPronto: 'bg-estado-preparo text-texto-invertido hover:opacity-90',
  acaoDespachar: 'bg-sucesso text-texto-invertido hover:opacity-90',
}

const TAMANHOS: Record<Tamanho, string> = {
  medio: 'h-toque px-4 text-corpo',
  grande: 'h-12 px-6 text-titulo',
}

interface BotaoProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variante?: Variante
  tamanho?: Tamanho
  carregando?: boolean
  larguraTotal?: boolean
  children: ReactNode
}

export function Botao({
  variante = 'primario',
  tamanho = 'medio',
  carregando = false,
  larguraTotal = false,
  disabled,
  className,
  children,
  ...props
}: BotaoProps) {
  return (
    <button
      {...props}
      disabled={disabled || carregando}
      className={cn(
        'inline-flex items-center justify-center gap-2 rounded-controle font-medium',
        'transition-colors outline-offset-2 focus-visible:outline-2 focus-visible:outline-marca-600',
        'disabled:cursor-not-allowed disabled:opacity-50',
        VARIANTES[variante],
        TAMANHOS[tamanho],
        larguraTotal && 'w-full',
        className,
      )}
    >
      {carregando && (
        <span
          aria-hidden
          className="size-4 animate-spin rounded-full border-2 border-current border-t-transparent"
        />
      )}
      {children}
    </button>
  )
}
