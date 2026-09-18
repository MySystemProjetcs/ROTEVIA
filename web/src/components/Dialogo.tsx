import { useEffect, useRef } from 'react'
import type { ReactNode } from 'react'
import { cn } from '@/lib/cn'

interface DialogoProps {
  aberto: boolean
  onFechar: () => void
  titulo: string
  children: ReactNode
  className?: string
}

// <dialog> nativo com showModal(): o navegador entrega prisão de foco, Escape,
// inerte no resto da página e camada de topo. Recriar isso em <div> é um dos
// jeitos mais fáceis de quebrar acessibilidade sem perceber.
export function Dialogo({ aberto, onFechar, titulo, children, className }: DialogoProps) {
  const ref = useRef<HTMLDialogElement>(null)

  useEffect(() => {
    const dialogo = ref.current
    if (!dialogo) return

    if (aberto && !dialogo.open) dialogo.showModal()
    if (!aberto && dialogo.open) dialogo.close()
  }, [aberto])

  return (
    <dialog
      ref={ref}
      aria-label={titulo}
      // O Escape do navegador fecha sozinho; o close avisa o React para o
      // estado não ficar dizendo "aberto" com o diálogo já fechado.
      onClose={onFechar}
      // Clique no backdrop: o alvo é o próprio <dialog> só quando o clique cai
      // fora do conteúdo, porque o conteúdo é um filho.
      onClick={(evento) => {
        if (evento.target === ref.current) onFechar()
      }}
      className={cn(
        'm-auto w-[min(32rem,calc(100vw-2rem))] rounded-cartao border border-borda bg-superficie p-0 text-texto shadow-elevado backdrop:bg-texto/40',
        className,
      )}
    >
      {/* Só monta o conteúdo com o diálogo aberto: evita requisição e estado
          vivos atrás de uma janela que ninguém está vendo. */}
      {aberto && children}
    </dialog>
  )
}
