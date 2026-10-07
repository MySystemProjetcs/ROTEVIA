import { useEffect, useId, useLayoutEffect, useRef, useState } from 'react'
import { IconeMenu } from '@/components/icones/IconeMenu'
import { cn } from '@/lib/cn'

export interface AcaoDeMenu {
  chave: string
  rotulo: string
  /** Destaca em tom de perigo — para ações que removem algo. */
  perigosa?: boolean
  aoEscolher: () => void
}

// Menu de ações atrás de um hambúrguer. Irmão do <Menu>, não o mesmo: aquele
// escolhe um valor (menuitemradio, mostra o selecionado), este dispara ações
// (menuitem, sem estado). Misturar os dois num componente só deixaria metade
// das props inúteis em cada uso.
//
// Posicionamento fixed, não absolute: a tabela onde ele é usado vive dentro de
// um container com overflow-x-auto, e o CSS computa o outro eixo como auto
// junto — um dropdown absoluto seria recortado nas últimas linhas.
export function MenuDeAcoes({
  rotulo,
  acoes,
  className,
}: {
  rotulo: string
  acoes: AcaoDeMenu[]
  className?: string
}) {
  const [aberto, setAberto] = useState(false)
  const [emFoco, setEmFoco] = useState(0)
  const [posicao, setPosicao] = useState<{ top: number; right: number } | null>(null)
  const gatilhoRef = useRef<HTMLButtonElement>(null)
  const menuRef = useRef<HTMLDivElement>(null)
  const itensRef = useRef<(HTMLButtonElement | null)[]>([])
  const idDoMenu = useId()

  // Ancora no gatilho antes da pintura, senão o menu aparece no canto e salta.
  useLayoutEffect(() => {
    if (!aberto) return

    const caixa = gatilhoRef.current?.getBoundingClientRect()
    if (caixa) {
      setPosicao({
        top: caixa.bottom + 4,
        right: Math.max(window.innerWidth - caixa.right, 8),
      })
    }
  }, [aberto])

  // Clique fora e Escape fecham. Rolagem e redimensionamento também: com
  // position fixed o menu não acompanha o gatilho, então ficaria solto na tela.
  useEffect(() => {
    if (!aberto) return

    function aoClicarFora(evento: MouseEvent) {
      const alvo = evento.target as Node
      if (!gatilhoRef.current?.contains(alvo) && !menuRef.current?.contains(alvo)) {
        setAberto(false)
      }
    }

    function fechar() {
      setAberto(false)
    }

    document.addEventListener('mousedown', aoClicarFora)
    window.addEventListener('resize', fechar)
    // capture: pega a rolagem de qualquer container, não só a da janela.
    window.addEventListener('scroll', fechar, true)

    return () => {
      document.removeEventListener('mousedown', aoClicarFora)
      window.removeEventListener('resize', fechar)
      window.removeEventListener('scroll', fechar, true)
    }
  }, [aberto])

  // Move o foco do teclado para o item destacado sempre que ele muda.
  useEffect(() => {
    if (aberto) itensRef.current[emFoco]?.focus()
  }, [aberto, emFoco])

  function abrir() {
    setEmFoco(0)
    setAberto(true)
  }

  function escolher(acao: AcaoDeMenu) {
    setAberto(false)
    acao.aoEscolher()
  }

  function aoTeclar(evento: React.KeyboardEvent) {
    if (evento.key === 'Escape') {
      setAberto(false)
      gatilhoRef.current?.focus()
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
      setEmFoco((i) => (i + 1) % acoes.length)
    } else if (evento.key === 'ArrowUp') {
      evento.preventDefault()
      setEmFoco((i) => (i - 1 + acoes.length) % acoes.length)
    } else if (evento.key === 'Home') {
      evento.preventDefault()
      setEmFoco(0)
    } else if (evento.key === 'End') {
      evento.preventDefault()
      setEmFoco(acoes.length - 1)
    }
  }

  return (
    <div className={cn('inline-flex', className)} onKeyDown={aoTeclar}>
      <button
        ref={gatilhoRef}
        type="button"
        aria-haspopup="menu"
        aria-expanded={aberto}
        aria-controls={idDoMenu}
        aria-label={rotulo}
        title={rotulo}
        onClick={() => (aberto ? setAberto(false) : abrir())}
        className="inline-flex size-8 items-center justify-center rounded-controle text-texto-suave outline-offset-2 transition-colors hover:bg-superficie-alt hover:text-texto focus-visible:outline-2 focus-visible:outline-marca-600"
      >
        <IconeMenu className="size-5" />
      </button>

      {aberto && posicao && (
        <div
          ref={menuRef}
          id={idDoMenu}
          role="menu"
          aria-label={rotulo}
          style={{ position: 'fixed', top: posicao.top, right: posicao.right }}
          className="z-50 flex min-w-44 flex-col overflow-hidden rounded-controle border border-borda bg-superficie shadow-elevado"
        >
          {acoes.map((acao, indice) => (
            <button
              key={acao.chave}
              ref={(el) => {
                itensRef.current[indice] = el
              }}
              type="button"
              role="menuitem"
              // tabIndex -1: a navegação dentro do menu é por seta, não por Tab.
              tabIndex={-1}
              onClick={() => escolher(acao)}
              className={cn(
                'px-3 py-2.5 text-left text-corpo transition-colors hover:bg-superficie-alt focus-visible:bg-superficie-alt focus-visible:outline-none',
                acao.perigosa ? 'text-perigo' : 'text-texto',
              )}
            >
              {acao.rotulo}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
