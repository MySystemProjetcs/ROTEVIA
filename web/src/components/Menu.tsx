import { useEffect, useId, useRef, useState } from 'react'
import { cn } from '@/lib/cn'

export interface OpcaoDeMenu<T extends string> {
  valor: T
  rotulo: string
}

interface MenuProps<T extends string> {
  rotulo: string
  valor: T
  opcoes: readonly OpcaoDeMenu<T>[]
  onEscolher: (valor: T) => void
  className?: string
}

// Dropdown acessível montado sobre <button> + lista com roles de menu: teclado
// (setas, Enter, Escape, Home/End), foco gerenciado e fechamento ao clicar
// fora. Nativo de propósito — <select> não deixa estilizar a lista, e trazer
// biblioteca de UI significaria um segundo sistema de tema ao lado dos tokens.
export function Menu<T extends string>({
  rotulo,
  valor,
  opcoes,
  onEscolher,
  className,
}: MenuProps<T>) {
  const [aberto, setAberto] = useState(false)
  const [emFoco, setEmFoco] = useState(0)
  const containerRef = useRef<HTMLDivElement>(null)
  const itensRef = useRef<(HTMLButtonElement | null)[]>([])
  const idDoMenu = useId()

  const selecionada = opcoes.find((o) => o.valor === valor)

  // Clique fora e Escape fecham. Sem isso o menu ficaria preso aberto quando a
  // pessoa desiste e clica em outro lugar da tela.
  useEffect(() => {
    if (!aberto) return

    function aoClicarFora(evento: MouseEvent) {
      if (!containerRef.current?.contains(evento.target as Node)) setAberto(false)
    }

    document.addEventListener('mousedown', aoClicarFora)
    return () => document.removeEventListener('mousedown', aoClicarFora)
  }, [aberto])

  // Move o foco do teclado para o item destacado sempre que ele muda.
  useEffect(() => {
    if (aberto) itensRef.current[emFoco]?.focus()
  }, [aberto, emFoco])

  function abrir() {
    const atual = opcoes.findIndex((o) => o.valor === valor)
    setEmFoco(atual >= 0 ? atual : 0)
    setAberto(true)
  }

  function escolher(opcao: OpcaoDeMenu<T>) {
    onEscolher(opcao.valor)
    setAberto(false)
  }

  function aoTeclar(evento: React.KeyboardEvent) {
    if (evento.key === 'Escape') {
      setAberto(false)
      return
    }

    if (!aberto && (evento.key === 'ArrowDown' || evento.key === 'Enter' || evento.key === ' ')) {
      evento.preventDefault()
      abrir()
      return
    }

    if (!aberto) return

    if (evento.key === 'ArrowDown') {
      evento.preventDefault()
      setEmFoco((i) => (i + 1) % opcoes.length)
    } else if (evento.key === 'ArrowUp') {
      evento.preventDefault()
      setEmFoco((i) => (i - 1 + opcoes.length) % opcoes.length)
    } else if (evento.key === 'Home') {
      evento.preventDefault()
      setEmFoco(0)
    } else if (evento.key === 'End') {
      evento.preventDefault()
      setEmFoco(opcoes.length - 1)
    }
  }

  return (
    <div ref={containerRef} className={cn('relative', className)} onKeyDown={aoTeclar}>
      <button
        type="button"
        aria-haspopup="menu"
        aria-expanded={aberto}
        aria-controls={idDoMenu}
        onClick={() => (aberto ? setAberto(false) : abrir())}
        className="flex h-toque w-full items-center justify-between gap-2 rounded-controle border border-borda-forte bg-superficie px-3 text-corpo text-texto outline-offset-2 transition-colors hover:bg-superficie-alt focus-visible:outline-2 focus-visible:outline-marca-600"
      >
        <span>{selecionada?.rotulo ?? rotulo}</span>
        <span aria-hidden className="text-texto-fraco">
          ▾
        </span>
      </button>

      {aberto && (
        <div
          id={idDoMenu}
          role="menu"
          aria-label={rotulo}
          className="absolute z-10 mt-1 flex w-full flex-col overflow-hidden rounded-controle border border-borda bg-superficie shadow-elevado"
        >
          {opcoes.map((opcao, indice) => (
            <button
              key={opcao.valor}
              ref={(el) => {
                itensRef.current[indice] = el
              }}
              type="button"
              role="menuitemradio"
              aria-checked={opcao.valor === valor}
              // tabIndex -1: a navegação dentro do menu é por seta, não por Tab.
              tabIndex={-1}
              onClick={() => escolher(opcao)}
              className={cn(
                'px-3 py-2.5 text-left text-corpo transition-colors hover:bg-superficie-alt focus-visible:bg-superficie-alt focus-visible:outline-none',
                opcao.valor === valor ? 'font-semibold text-marca-600' : 'text-texto',
              )}
            >
              {opcao.rotulo}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
