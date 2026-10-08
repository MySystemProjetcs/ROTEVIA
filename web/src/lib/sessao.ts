import type { PapelUsuario } from '@/dominio/pedido'

// O token vive em memória como fonte primária. localStorage está proibido
// (CLAUDE.md §10): quebra na origem capacitor:// quando a base virar app.
// Para sobreviver ao refresh na mesma aba, espelhamos em sessionStorage —
// escopo de aba (fechou a aba, deslogou), mesma API síncrona, e quando o
// Capacitor entrar a troca continua sendo só nestas funções (por
// SecureStorage), nenhuma tela muda.
let tokenEmMemoria: string | null = null

const CHAVE_TOKEN = 'toolsdelivery.access_token'
const CHAVE_SESSAO = 'toolsdelivery.sessao'

function lerStorage(chave: string): string | null {
  try {
    return typeof sessionStorage === 'undefined' ? null : sessionStorage.getItem(chave)
  } catch {
    return null
  }
}

function escreverStorage(chave: string, valor: string) {
  try {
    sessionStorage.setItem(chave, valor)
  } catch {
    // Storage indisponível (modo privado, etc.): a sessão vale só na memória.
  }
}

function removerStorage(chave: string) {
  try {
    sessionStorage.removeItem(chave)
  } catch {
    // Nada a fazer: sem storage não há o que limpar.
  }
}

export function guardarToken(token: string) {
  tokenEmMemoria = token
  escreverStorage(CHAVE_TOKEN, token)
}

export function lerToken(): string | null {
  if (!tokenEmMemoria) {
    // Pós-refresh: a memória zerou, reidrata do espelho da aba.
    tokenEmMemoria = lerStorage(CHAVE_TOKEN)
  }

  return tokenEmMemoria
}

export function limparToken() {
  tokenEmMemoria = null
  removerStorage(CHAVE_TOKEN)
}

export interface DadosDoToken {
  email: string
  papel: PapelUsuario
  merchantId: string | null
  nomeRestaurante: string | null
  nomeUsuario: string
  expiraEm: Date
}

const CLAIM_PAPEL = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'
const CLAIM_EMAIL = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'

// Leitura apenas para decidir o que a interface mostra. Quem valida o token é
// o backend — nada aqui é decisão de segurança.
export function lerDadosDoToken(token: string): DadosDoToken | null {
  try {
    const corpo = token.split('.')[1]
    const json = atob(corpo.replace(/-/g, '+').replace(/_/g, '/'))
    const dados = JSON.parse(json)

    return {
      email: dados[CLAIM_EMAIL] ?? dados.email ?? '',
      papel: dados[CLAIM_PAPEL] ?? dados.role,
      merchantId: dados.merchant_id ?? null,
      nomeRestaurante: null,
      nomeUsuario: '',
      expiraEm: new Date(dados.exp * 1000),
    }
  } catch {
    return null
  }
}

interface SessaoPersistida {
  usuario: DadosDoToken
  deveTrocarSenha: boolean
}

// O nome do restaurante não viaja no JWT, só na resposta do login — por isso
// o usuário logado é persistido junto do token. Sem isso, o refresh voltaria
// a exibir o e-mail no header.
export function guardarSessao(usuario: DadosDoToken, deveTrocarSenha: boolean) {
  escreverStorage(CHAVE_SESSAO, JSON.stringify({ usuario, deveTrocarSenha }))
}

export function lerSessao(): SessaoPersistida | null {
  const bruto = lerStorage(CHAVE_SESSAO)
  if (!bruto || !lerToken()) return null

  try {
    const dados = JSON.parse(bruto) as {
      usuario?: {
        email?: unknown
        papel?: unknown
        merchantId?: unknown
        nomeRestaurante?: unknown
        nomeUsuario?: unknown
        expiraEm?: unknown
      }
      deveTrocarSenha?: unknown
    }
    const brutoUsuario = dados.usuario
    // JSON.stringify serializa Date como string ISO — String() a preserva, e
    // qualquer outro formato cai em Invalid Date e invalida a restauração.
    const expiraEm = new Date(String(brutoUsuario?.expiraEm ?? ''))

    if (typeof brutoUsuario?.email !== 'string' || brutoUsuario.email === '') return null
    if (
      brutoUsuario?.papel !== 'AdministradorSistema' &&
      brutoUsuario?.papel !== 'DonoRestaurante' &&
      brutoUsuario?.papel !== 'Entregador'
    )
      return null
    if (Number.isNaN(expiraEm.getTime())) return null
    if (expiraEm.getTime() <= Date.now()) {
      limparSessao()
      return null
    }

    return {
      usuario: {
        email: brutoUsuario.email,
        papel: brutoUsuario.papel,
        merchantId: typeof brutoUsuario.merchantId === 'string' ? brutoUsuario.merchantId : null,
        nomeRestaurante: typeof brutoUsuario.nomeRestaurante === 'string' ? brutoUsuario.nomeRestaurante : null,
        nomeUsuario: typeof brutoUsuario.nomeUsuario === 'string' ? brutoUsuario.nomeUsuario : '',
        expiraEm,
      },
      deveTrocarSenha: dados.deveTrocarSenha === true,
    }
  } catch {
    return null
  }
}

export function limparSessao() {
  limparToken()
  removerStorage(CHAVE_SESSAO)
}
