import type { IconeProps } from './tipos'

export function IconeMoeda({ className }: IconeProps) {
  return (
    <svg viewBox="0 0 24 24" fill="none" className={className} aria-hidden>
      <circle cx="12" cy="12" r="8.5" stroke="currentColor" strokeWidth="1.8" />
      <path
        d="M12 7.5v9M9.2 9.3c0-1 1.2-1.8 2.8-1.8s2.8.8 2.8 1.8-1.1 1.6-2.8 1.9c-1.7.3-2.8.9-2.8 1.9s1.2 1.8 2.8 1.8 2.8-.8 2.8-1.8"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinecap="round"
      />
    </svg>
  )
}
