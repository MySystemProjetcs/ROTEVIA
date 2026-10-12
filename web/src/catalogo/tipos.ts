// Canais de catálogo. Espelha a estrutura de `horarios/tipos.ts` de
// propósito: é a mesma ideia de "um painel por plataforma", e divergir a
// modelagem entre as duas páginas só criaria duas formas de dizer a mesma
// coisa.

export type CanalCatalogo = 'ifood' | 'noventaenove' | 'propria'

export interface CanalCatalogoInfo {
  canal: CanalCatalogo
  rotulo: string
  // Se o backend já serve o catálogo deste canal. Quando false, a aba abre
  // explicando o que falta em vez de mostrar lista vazia — lista vazia o
  // lojista lê como "perdi meus itens".
  disponivel: boolean
  // O que bloqueia, para a aba conseguir explicar sem o usuário abrir
  // chamado. Nulo quando disponivel é true.
  bloqueio: string | null
}

export const CANAIS_CATALOGO: readonly CanalCatalogoInfo[] = [
  {
    canal: 'ifood',
    rotulo: 'iFood',
    disponivel: false,
    bloqueio:
      'O módulo de catálogo precisa ser habilitado para este aplicativo no portal do iFood. '
      + 'Hoje o app tem apenas pedidos, eventos e loja.',
  },
  {
    canal: 'noventaenove',
    rotulo: '99Food',
    disponivel: false,
    bloqueio:
      'A loja ainda não está autorizada para este aplicativo na 99Food. '
      + 'Conclua a autorização em Configurações › 99Food.',
  },
  {
    canal: 'propria',
    rotulo: 'Loja própria',
    disponivel: false,
    bloqueio: 'O catálogo próprio ainda não tem backend.',
  },
]

// Resultado do diagnóstico de escopo do iFood (GET /diagnostics/ifood-catalog).
// Existe para a aba do iFood conseguir dizer se o bloqueio continua de pé sem
// o lojista precisar do console do navegador.
export interface DiagnosticoCatalogoIFood {
  escopoLiberado: boolean
  quantidade: number
  catalogos: {
    catalogId: string
    status: string | null
    context: string[]
    modifiedAt: string | null
  }[]
}
