import type { IconeProps } from './tipos'

export function IconeOlhoRiscado({ className }: IconeProps) {
  return (
    <svg viewBox="0 0 24 24" fill="none" className={className} aria-hidden>
      <path
        d="M2.5 12S6 5.5 12 5.5c1.6 0 3 .47 4.2 1.14M21.5 12s-1.2 2.24-3.5 4.03M9.2 9.3A3.25 3.25 0 0 0 12 15.25c.8 0 1.53-.29 2.1-.77"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <path d="M4.5 4.5l15 15" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
    </svg>
  )
}
