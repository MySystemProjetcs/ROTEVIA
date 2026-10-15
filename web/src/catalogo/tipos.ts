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
    // Status real do iFood não é fixo: depende da chamada a /catalogo/status,
    // que distingue escopo liberado (200), módulo não habilitado (403), loja
    // não conectada (409) e falhas de rede. O painel decide; aqui ficaria
    // mentindo em qualquer sentido.
    disponivel: true,
    bloqueio: null,
  },
  {
    canal: 'noventaenove',
    rotulo: '99Food',
    disponivel: true,
    bloqueio: null,
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

// Item do catálogo do iFood. Preço já vem em reais — a doc do iFood confirma
// que não é centavo, diferente da 99Food. precoOriginal só vem quando difere
// de preco (item com desconto); null quando não há promoção. restricoes carrega
// ITEM_PAUSED/ITEM_HAS_VIOLATION pro painel explicar por que algo vendável
// pode ter indicação adicional.
export interface ItemCatalogoIFood {
  itemId: string
  nome: string
  descricao: string | null
  preco: number
  precoOriginal: number | null
  disponivel: boolean
  restricoes: string[]
  imagemUrl: string | null
}

export interface CategoriaCatalogoIFood {
  categoryId: string
  nome: string
  pausada: boolean
  restricoes: string[]
  itens: ItemCatalogoIFood[]
}

export interface CatalogoCompletoIFood {
  catalogId: string
  groupId: string
  totalItens: number
  categorias: CategoriaCatalogoIFood[]
}

// Cardápio da 99Food (GET /restaurantes/{id}/catalogo/99food). Preço já vem
// em reais: a conversão de centavos acontece no client do backend, para que
// a unidade da 99 não vaze até aqui.
export interface ItemCardapio {
  appItemId: string
  nome: string
  descricao: string | null
  preco: number
  disponivel: boolean
  vendidoSeparadamente: boolean
  imagemUrl: string | null
}

export interface Categoria99 {
  appCategoryId: string
  nome: string
  itens: ItemCardapio[]
}

export interface Cardapio99 {
  totalItens: number
  categorias: Categoria99[]
  // Item sem categoria é caso real na 99 ("can only belong to one category or
  // no category") e sumiria da tela se só renderizássemos categorias.
  itensSemCategoria: ItemCardapio[]
}

// A 99 aceita o lote parcialmente — por isso a resposta separa o que passou
// do que não passou, em vez de um booleano.
export interface ResultadoDisponibilidade {
  sucesso: string[]
  falha: string[]
}
