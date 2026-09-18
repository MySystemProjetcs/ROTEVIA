import { createContext, useContext } from 'react'

export type EstadoDaBarra = 'expandida' | 'recolhida'

export interface ValorDaBarraLateral {
  /** Como a barra está no desktop. No celular ela é sempre expandida quando abre. */
  estado: EstadoDaBarra
  /** Desktop: expandida ou recolhida ao trilho de ícones. */
  expandida: boolean
  definirExpandida: (expandida: boolean) => void
  /** Celular: gaveta aberta por cima do conteúdo. Estado próprio, de propósito —
   *  recolher no desktop e fechar no celular são gestos diferentes. */
  abertaNoCelular: boolean
  definirAbertaNoCelular: (aberta: boolean) => void
  ehCelular: boolean
  /** Um gesto só para o botão e o atalho: faz a coisa certa em cada tamanho. */
  alternar: () => void
}

export const ContextoDaBarraLateral = createContext<ValorDaBarraLateral | null>(null)

export function useBarraLateral(): ValorDaBarraLateral {
  const contexto = useContext(ContextoDaBarraLateral)
  if (!contexto) throw new Error('useBarraLateral precisa estar dentro de BarraLateralProvider.')

  return contexto
}
