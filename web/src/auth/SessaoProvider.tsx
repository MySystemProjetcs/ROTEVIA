import { createContext, useCallback, useContext, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { api } from '@/lib/api'
import { guardarSessao, guardarToken, lerDadosDoToken, lerSessao, limparSessao } from '@/lib/sessao'
import type { DadosDoToken } from '@/lib/sessao'

interface RespostaLogin {
  accessToken: string
  expiraEm: string
  deveTrocarSenha: boolean
  nomeRestaurante: string | null
  nomeUsuario: string
}

interface Sessao {
  usuario: DadosDoToken | null
  deveTrocarSenha: boolean
  entrar: (email: string, senha: string) => Promise<void>
  sair: () => void
}

const ContextoDaSessao = createContext<Sessao | null>(null)

export function SessaoProvider({ children }: { children: ReactNode }) {
  // Inicializador lazy: restaura a sessão da aba de forma síncrona, antes da
  // primeira renderização — sem flash da tela de login no refresh.
  const [sessaoInicial] = useState(lerSessao)
  const [usuario, setUsuario] = useState<DadosDoToken | null>(sessaoInicial?.usuario ?? null)
  const [deveTrocarSenha, setDeveTrocarSenha] = useState(sessaoInicial?.deveTrocarSenha ?? false)

  const entrar = useCallback(async (email: string, senha: string) => {
    const resposta = await api.post<RespostaLogin>('/auth/login', { email, senha })

    guardarToken(resposta.accessToken)
    const dados = lerDadosDoToken(resposta.accessToken)
    const logado = dados
      ? { ...dados, nomeRestaurante: resposta.nomeRestaurante ?? null, nomeUsuario: resposta.nomeUsuario ?? '' }
      : null
    setUsuario(logado)
    setDeveTrocarSenha(resposta.deveTrocarSenha)

    if (logado) guardarSessao(logado, resposta.deveTrocarSenha)
  }, [])

  const sair = useCallback(() => {
    limparSessao()
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
