import type { IconeProps } from './tipos'

export function IconeSair({ className }: IconeProps) {
  return (
    <svg viewBox="0 0 24 24" fill="none" className={className} aria-hidden>
      <path
        d="M14 4.5h4.25A1.25 1.25 0 0 1 19.5 5.75v12.5a1.25 1.25 0 0 1-1.25 1.25H14M10 15.5 13.5 12 10 8.5M13.5 12h-9"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  )
}
