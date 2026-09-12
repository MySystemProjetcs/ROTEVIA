import type { PapelUsuario } from '@/dominio/pedido'

// O token vive em memória. localStorage está proibido (CLAUDE.md §10): quebra
// na origem capacitor:// quando a base virar app. Quando o Capacitor entrar, a
// troca é substituir estas duas funções por SecureStorage — nenhuma tela muda.
let tokenEmMemoria: string | null = null

export function guardarToken(token: string) {
  tokenEmMemoria = token
}

export function lerToken(): string | null {
  return tokenEmMemoria
}

export function limparToken() {
  tokenEmMemoria = null
}

export interface DadosDoToken {
  email: string
  papel: PapelUsuario
  merchantId: string | null
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
      expiraEm: new Date(dados.exp * 1000),
    }
  } catch {
    return null
  }
}
