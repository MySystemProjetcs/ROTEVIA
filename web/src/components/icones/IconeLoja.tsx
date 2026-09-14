import type { IconeProps } from './tipos'

// Fachada com toldo — é a loja de onde a entrega sai. Preenchido, não só
// contorno: no mapa precisa ser legível sobre ruas e quarteirões. A porta é
// vazada por fill-rule, e não pintada de branco, para o ícone herdar uma cor
// só de quem o usa.
export function IconeLoja({ className }: IconeProps) {
  return (
    <svg viewBox="0 0 24 24" fill="none" className={className} aria-hidden>
      <path
        fill="currentColor"
        fillRule="evenodd"
        clipRule="evenodd"
        d="M4 9h16v10a1 1 0 0 1-1 1h-5v-5h-4v5H5a1 1 0 0 1-1-1V9Z"
      />
      <path
        fill="currentColor"
        d="M3.6 4h16.8l1.4 4.2a3 3 0 0 1-5.7.5 3 3 0 0 1-5.7 0 3 3 0 0 1-5.7-.5L3.6 4Z"
      />
    </svg>
  )
}
