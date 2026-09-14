import { cn } from '@/lib/cn'

function iniciaisDoNome(nome: string): string {
  const partes = nome.trim().split(/\s+/)
  return ((partes[0]?.[0] ?? '') + (partes[1]?.[0] ?? '')).toUpperCase()
}

// Avatar com iniciais, sem foto. A cor vem de fora (classe): na tabela de
// motoboys é a situação operacional, no header é o papel — o componente só
// desenha o círculo.
export function AvatarIniciais({ nome, className }: { nome: string; className?: string }) {
  return (
    <span
      aria-hidden
      className={cn(
        'flex size-10 shrink-0 items-center justify-center rounded-full border text-apoio font-bold',
        className,
      )}
    >
      {iniciaisDoNome(nome)}
    </span>
  )
}
