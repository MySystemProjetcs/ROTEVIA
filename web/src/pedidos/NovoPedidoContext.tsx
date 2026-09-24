import { createContext, useCallback, useContext, useRef } from 'react'
import type { ReactNode } from 'react'

// Permite que o botão "Novo pedido" do header (App.tsx) acione o formulário
// que vive dentro do PainelOperacao — sem prop drilling por três camadas.
interface NovoPedidoContexto {
  // O PainelOperacao registra aqui o callback que abre o formulário.
  registrar: (fn: () => void) => void
  // O header chama isto para abrir o formulário.
  abrir: () => void
}

const Contexto = createContext<NovoPedidoContexto | null>(null)

export function NovoPedidoProvider({ children }: { children: ReactNode }) {
  const callbackRef = useRef<(() => void) | null>(null)

  const registrar = useCallback((fn: () => void) => {
    callbackRef.current = fn
  }, [])

  const abrir = useCallback(() => {
    callbackRef.current?.()
  }, [])

  return <Contexto.Provider value={{ registrar, abrir }}>{children}</Contexto.Provider>
}

export function useNovoPedido() {
  const ctx = useContext(Contexto)
  if (!ctx) throw new Error('useNovoPedido precisa estar dentro de NovoPedidoProvider')
  return ctx
}
