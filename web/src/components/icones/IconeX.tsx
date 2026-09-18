import type { IconeProps } from './tipos'

export function IconeX({ className }: IconeProps) {
  return (
    <svg viewBox="0 0 24 24" fill="none" className={className} aria-hidden>
      <path
        d="M6.5 6.5l11 11M17.5 6.5l-11 11"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinecap="round"
      />
    </svg>
  )
}
