import type { IconeProps } from './tipos'

// Bolha de conversa genérica, não o marco registrado do WhatsApp — o rótulo
// de texto ao lado já deixa claro o que é.
export function IconeWhatsApp({ className }: IconeProps) {
  return (
    <svg viewBox="0 0 24 24" fill="none" className={className} aria-hidden>
      <path
        d="M12 4a8 8 0 0 0-6.93 12.02L4 20l4.13-1.05A8 8 0 1 0 12 4Z"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinejoin="round"
      />
      <path
        d="M9 10.5c0 3 1.5 4.5 4.5 4.5.4 0 .78-.15 1.06-.44l.6-.62a.7.7 0 0 0 .1-.85l-.63-1.06a.7.7 0 0 0-.86-.28l-.77.3a4.4 4.4 0 0 1-2.05-2.05l.3-.77a.7.7 0 0 0-.28-.86l-1.06-.63a.7.7 0 0 0-.85.1l-.62.6A1.5 1.5 0 0 0 9 10.5Z"
        fill="currentColor"
      />
    </svg>
  )
}
