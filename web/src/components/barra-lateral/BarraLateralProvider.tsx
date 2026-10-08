import { useCallback, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { useEhDesktop } from '@/lib/useEhDesktop'
import { ContextoDaBarraLateral } from './contexto'

const CHAVE = 'toolsdelivery.barra-lateral'

// Cmd+B no Mac, Ctrl+B no resto. Mesmo atalho do modelo do shadcn, e o mesmo
// que editores usam para a barra lateral — quem trabalha o dia todo no painel
// não tira a mão do teclado para recolher uma coluna.
const TECLA_DE_ATALHO = 'b'

function lerPreferencia(): boolean | null {
  try {
    const guardado = localStorage.getItem(CHAVE)
    return guardado === null ? null : guardado === 'expandida'
  } catch {
    // Navegador com dados de site bloqueados: cai no padrão.
    return null
  }
}

export function BarraLateralProvider({ children }: { children: ReactNode }) {
  const ehDesktop = useEhDesktop()
  const ehCelular = !ehDesktop

  // A escolha da pessoa vale mais que o padrão; sem escolha, expandida no
  // desktop. Inicializador lazy para não piscar recolhida antes de um efeito
  // corrigir.
  const [expandida, definirExpandida] = useState(() => lerPreferencia() ?? true)
  const [abertaNoCelular, definirAbertaNoCelular] = useState(false)

  // Persiste só o estado do desktop. Gaveta de celular aberta é situação do
  // momento, não preferência: reabrir o app já com ela aberta seria errado.
  useEffect(() => {
    try {
      localStorage.setItem(CHAVE, expandida ? 'expandida' : 'recolhida')
    } catch {
      // Não poder guardar a preferência não é motivo para quebrar a tela.
    }
  }, [expandida])

  const alternar = useCallback(() => {
    if (ehCelular) {
      definirAbertaNoCelular((a) => !a)
      return
    }

    definirExpandida((e) => !e)
  }, [ehCelular])

  useEffect(() => {
    function aoTeclar(evento: KeyboardEvent) {
      if (evento.key.toLowerCase() !== TECLA_DE_ATALHO) return
      if (!evento.metaKey && !evento.ctrlKey) return

      evento.preventDefault()
      alternar()
    }

    window.addEventListener('keydown', aoTeclar)
    return () => window.removeEventListener('keydown', aoTeclar)
  }, [alternar])

  // Escape fecha a gaveta do celular, onde ela cobre a tela inteira. No desktop
  // não faz nada: recolher a coluna não é sair de um modo.
  useEffect(() => {
    if (!abertaNoCelular) return

    function aoTeclar(evento: KeyboardEvent) {
      if (evento.key === 'Escape') definirAbertaNoCelular(false)
    }

    document.addEventListener('keydown', aoTeclar)
    return () => document.removeEventListener('keydown', aoTeclar)
  }, [abertaNoCelular])

  const valor = useMemo(
    () => ({
      estado: expandida ? ('expandida' as const) : ('recolhida' as const),
      expandida,
      definirExpandida,
      abertaNoCelular,
      definirAbertaNoCelular,
      ehCelular,
      alternar,
    }),
    [expandida, abertaNoCelular, ehCelular, alternar],
  )

  return <ContextoDaBarraLateral.Provider value={valor}>{children}</ContextoDaBarraLateral.Provider>
}
