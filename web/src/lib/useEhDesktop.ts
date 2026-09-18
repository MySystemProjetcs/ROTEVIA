import { useEffect, useState } from 'react'

// Mesmo ponto de quebra do `md:` do Tailwind. Duplicado aqui de propósito: o
// JavaScript precisa da mesma fronteira que o CSS usa, e não há como ler a
// configuração do Tailwind em tempo de execução.
const CONSULTA = '(min-width: 768px)'

// A barra lateral nasce aberta no desktop e fechada no celular, e no desktop
// navegar não fecha a barra. Nada disso dá para decidir só com classe CSS.
export function useEhDesktop(): boolean {
  const [ehDesktop, setEhDesktop] = useState(
    // Inicializador lazy: acerta já na primeira renderização, sem a barra
    // piscar fechada antes de um efeito corrigir.
    () => typeof window !== 'undefined' && window.matchMedia(CONSULTA).matches,
  )

  useEffect(() => {
    const consulta = window.matchMedia(CONSULTA)
    const aoMudar = (evento: MediaQueryListEvent) => setEhDesktop(evento.matches)

    consulta.addEventListener('change', aoMudar)
    return () => consulta.removeEventListener('change', aoMudar)
  }, [])

  return ehDesktop
}
