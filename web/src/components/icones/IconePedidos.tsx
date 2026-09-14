import type { IconeProps } from './tipos'

// Sem lib de ícone no projeto (CLAUDE.md §10 pede pra perguntar antes de
// instalar pacote) — SVG inline, mesmo espírito do logo em Logo.tsx.
// currentColor: herda o token de cor de quem usa o ícone, nunca hex fixo.
export function IconePedidos({ className }: IconeProps) {
  return (
    <svg viewBox="0 0 24 24" fill="none" className={className} aria-hidden>
      <path
        d="M4 7h16l-1.2 11.2a2 2 0 0 1-2 1.8H7.2a2 2 0 0 1-2-1.8L4 7Z"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinejoin="round"
      />
      <path d="M8 7V5a4 4 0 0 1 8 0v2" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
    </svg>
  )
}
