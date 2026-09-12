import marca from '@/assets/rotevia.svg'
import { cn } from '@/lib/cn'

const NOME = 'ROTEVIA'

type TamanhoLogo = 'compacto' | 'medio' | 'grande'

// As cores da marca vivem no próprio arquivo SVG, não aqui: o componente só
// decide tamanho e composição.
const SIMBOLO: Record<TamanhoLogo, string> = {
  compacto: 'size-8',
  medio: 'size-10',
  grande: 'size-14',
}

const TEXTO: Record<TamanhoLogo, string> = {
  compacto: 'text-titulo',
  medio: 'text-titulo',
  grande: 'text-destaque',
}

interface LogoProps {
  tamanho?: TamanhoLogo
  /** Só o símbolo, sem o nome ao lado. */
  apenasSimbolo?: boolean
  className?: string
}

export function Logo({ tamanho = 'medio', apenasSimbolo = false, className }: LogoProps) {
  return (
    <span className={cn('inline-flex items-center gap-2.5', className)}>
      <img src={marca} alt={apenasSimbolo ? NOME : ''} className={cn(SIMBOLO[tamanho], 'shrink-0')} />
      {!apenasSimbolo && (
        <span className={cn('font-bold tracking-tight text-texto', TEXTO[tamanho])}>{NOME}</span>
      )}
    </span>
  )
}
