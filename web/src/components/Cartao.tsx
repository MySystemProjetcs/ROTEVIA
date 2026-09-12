import type { ComponentPropsWithRef, ReactNode } from 'react'
import { cn } from '@/lib/cn'

type Elevacao = 'plana' | 'elevada'

const ELEVACOES: Record<Elevacao, string> = {
  plana: 'shadow-cartao',
  elevada: 'shadow-elevado',
}

interface CartaoProps extends ComponentPropsWithRef<'div'> {
  elevacao?: Elevacao
  children: ReactNode
}

export function Cartao({ elevacao = 'plana', className, children, ...props }: CartaoProps) {
  return (
    <div
      {...props}
      className={cn(
        'rounded-cartao border border-borda bg-superficie',
        ELEVACOES[elevacao],
        className,
      )}
    >
      {children}
    </div>
  )
}

export function CartaoCabecalho({ className, children, ...props }: ComponentPropsWithRef<'div'>) {
  return (
    <div {...props} className={cn('flex items-start justify-between gap-3 p-4 pb-0', className)}>
      {children}
    </div>
  )
}

export function CartaoCorpo({ className, children, ...props }: ComponentPropsWithRef<'div'>) {
  return (
    <div {...props} className={cn('p-4', className)}>
      {children}
    </div>
  )
}

export function CartaoRodape({ className, children, ...props }: ComponentPropsWithRef<'div'>) {
  return (
    <div
      {...props}
      className={cn('flex items-center gap-2 border-t border-borda p-4', className)}
    >
      {children}
    </div>
  )
}
