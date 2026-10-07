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

/** Endereço se informa por CEP + número: o resto (logradouro, bairro, cidade) e
 *  a coordenada o servidor resolve sozinho. */
export interface NovoEnderecoDaLoja {
  cep: string
  numero: string
  complemento?: string
  /** Escape manual: só preenchidos quando o servidor não conseguiu localizar o
   *  endereço no mapa. Informados, vencem a busca automática. */
  latitude?: number | null
  longitude?: number | null
}

export interface ValorDoPerfil {
  perfil: Perfil | null
  carregando: boolean
  erro: string | null
  enviarFoto: (fotoBase64: string) => Promise<void>
  // As três edições do perfil. Devolvem a mensagem de erro (ou null em caso de
  // sucesso) em vez de lançar: cada linha do diálogo mostra o próprio erro ao
  // lado do campo, que é onde a pessoa está olhando.
  alterarEmail: (email: string) => Promise<string | null>
  alterarNomeDaLoja: (nome: string) => Promise<string | null>
  alterarEndereco: (novo: NovoEnderecoDaLoja) => Promise<string | null>
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
