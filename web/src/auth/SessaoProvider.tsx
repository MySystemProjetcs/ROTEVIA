import { createContext, useCallback, useContext, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { api } from '@/lib/api'
import { guardarToken, lerDadosDoToken, limparToken } from '@/lib/sessao'
import type { DadosDoToken } from '@/lib/sessao'

interface RespostaLogin {
  accessToken: string
  expiraEm: string
  deveTrocarSenha: boolean
}

interface Sessao {
  usuario: DadosDoToken | null
  deveTrocarSenha: boolean
  entrar: (email: string, senha: string) => Promise<void>
  sair: () => void
}

const ContextoDaSessao = createContext<Sessao | null>(null)

export function SessaoProvider({ children }: { children: ReactNode }) {
  const [usuario, setUsuario] = useState<DadosDoToken | null>(null)
  const [deveTrocarSenha, setDeveTrocarSenha] = useState(false)

  const entrar = useCallback(async (email: string, senha: string) => {
    const resposta = await api.post<RespostaLogin>('/auth/login', { email, senha })

    guardarToken(resposta.accessToken)
    setUsuario(lerDadosDoToken(resposta.accessToken))
    setDeveTrocarSenha(resposta.deveTrocarSenha)
  }, [])

  const sair = useCallback(() => {
    limparToken()
    setUsuario(null)
    setDeveTrocarSenha(false)
  }, [])

  const valor = useMemo(
    () => ({ usuario, deveTrocarSenha, entrar, sair }),
    [usuario, deveTrocarSenha, entrar, sair],
  )

  return <ContextoDaSessao.Provider value={valor}>{children}</ContextoDaSessao.Provider>
}

export function useSessao(): Sessao {
  const contexto = useContext(ContextoDaSessao)
  if (!contexto) throw new Error('useSessao precisa estar dentro de SessaoProvider.')

  return contexto
}
