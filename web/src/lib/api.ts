import { lerToken } from './sessao'

// Formato de erro do backend: ProblemDetails, com o código estável do domínio
// em "detail" e a mensagem para humano em "title" (ENGINEERING-GUIDE §3).
export class ErroDaApi extends Error {
  readonly status: number
  readonly codigo: string

  constructor(status: number, codigo: string, mensagem: string) {
    super(mensagem)
    this.status = status
    this.codigo = codigo
  }
}

async function requisitar<T>(caminho: string, init?: RequestInit): Promise<T> {
  const token = lerToken()

  const resposta = await fetch(`/api${caminho}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...init?.headers,
    },
  })

  if (!resposta.ok) {
    const problema = await resposta.json().catch(() => null)

    throw new ErroDaApi(
      resposta.status,
      problema?.detail ?? 'erro.desconhecido',
      problema?.title ?? 'Não foi possível completar a operação.',
    )
  }

  return resposta.status === 204 ? (undefined as T) : resposta.json()
}

export const api = {
  get: <T>(caminho: string) => requisitar<T>(caminho),
  post: <T>(caminho: string, corpo?: unknown) =>
    requisitar<T>(caminho, { method: 'POST', body: corpo ? JSON.stringify(corpo) : undefined }),
  put: <T>(caminho: string, corpo?: unknown) =>
    requisitar<T>(caminho, { method: 'PUT', body: corpo ? JSON.stringify(corpo) : undefined }),
  // `del` e não `delete`: delete é palavra reservada em JS.
  del: <T>(caminho: string) => requisitar<T>(caminho, { method: 'DELETE' }),
}
