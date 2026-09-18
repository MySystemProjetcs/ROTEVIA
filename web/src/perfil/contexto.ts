import { createContext, useContext } from 'react'

export interface PerfilDaLoja {
  nome: string
  endereco: string | null
  taxaPorEntrega: number
}

export interface PerfilDoEntregador {
  cpf: string
  telefone: string
  modeloDaMoto: string
  placa: string
  disponivelParaEntrega: boolean
}

export interface Perfil {
  nome: string
  email: string
  papel: string
  fotoBase64: string | null
  membroDesde: string
  loja: PerfilDaLoja | null
  entregador: PerfilDoEntregador | null
}

export interface ValorDoPerfil {
  perfil: Perfil | null
  carregando: boolean
  erro: string | null
  enviarFoto: (fotoBase64: string) => Promise<void>
}

export const ContextoDoPerfil = createContext<ValorDoPerfil | null>(null)

// Um único carregamento para a sessão inteira: a barra lateral, o cabeçalho e o
// diálogo mostram a mesma foto, e três buscas do mesmo perfil só produziriam
// três versões possíveis da mesma tela.
export function usePerfil(): ValorDoPerfil {
  const contexto = useContext(ContextoDoPerfil)
  if (!contexto) throw new Error('usePerfil precisa estar dentro de PerfilProvider.')

  return contexto
}
