import type { ReactNode } from 'react'
import { cn } from '@/lib/cn'

// Barra horizontal de ações, ao molde do <HorizontalMenu> do react-admin
// (MUI Tabs) mas nativa: reproduz a aparência (linha de itens, ativo com
// destaque) sem a dependência de framework (CLAUDE.md §10). Cada item é um
// botão porque na tela onde ele é usado hoje eles disparam ações, não
// navegação — se algum dia precisar virar link, troca por NavLink.
export interface ItemMenuHorizontal {
  chave: string
  rotulo: string
  icone?: ReactNode
  ativo?: boolean
  aoClicar: () => void
}

export function MenuHorizontal({
  itens,
  className,
  children,
}: {
  itens: ItemMenuHorizontal[]
  className?: string
  /** Espaço à direita do menu, na mesma linha — costuma hospedar um campo
   *  de busca ou um filtro; encolhe primeiro em telas estreitas. */
  children?: ReactNode
}) {
  return (
    <nav
      role="tablist"
      aria-orientation="horizontal"
      className={cn(
        'flex flex-wrap items-center gap-1 rounded-[14px] border border-borda bg-superficie-alt p-1',
        className,
      )}
    >
      {itens.map((item) => (
        <button
          key={item.chave}
          type="button"
          role="tab"
          aria-selected={item.ativo}
          onClick={item.aoClicar}
          className={cn(
            'inline-flex items-center gap-1.5 rounded-[10px] px-3 py-1.5 text-[13px] font-medium transition-colors',
            item.ativo
              ? 'bg-marca-600 text-white shadow-sm'
              : 'text-texto-suave hover:bg-superficie-afundada hover:text-texto',
          )}
        >
          {item.icone && <span className="shrink-0">{item.icone}</span>}
          {item.rotulo}
        </button>
      ))}

      {children && <div className="ml-auto flex min-w-0 items-center">{children}</div>}
    </nav>
  )
}
