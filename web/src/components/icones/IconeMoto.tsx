import type { IconeProps } from './tipos'

export function IconeMoto({ className }: IconeProps) {
  return (
    <svg viewBox="0 0 24 24" fill="none" className={className} aria-hidden>
      <circle cx="5.5" cy="17.5" r="2.5" stroke="currentColor" strokeWidth="1.8" />
      <circle cx="18.5" cy="17.5" r="2.5" stroke="currentColor" strokeWidth="1.8" />
      <path
        d="M8 17.5h7.5l-2-6h-4l-1-3H6"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <path
        d="M13.5 11.5 16 8h2.5"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <path d="M8.5 8.5h3" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
    </svg>
  )
}
