export type StatusPedido =
  | 'Recebido'
  | 'Confirmado'
  | 'EmPreparo'
  | 'Pronto'
  | 'Despachado'
  | 'Concluido'
  | 'Cancelado'

export type PapelUsuario = 'AdministradorSistema' | 'DonoRestaurante'

export interface ItemDoPedido {
  indice: number
  nome: string
  quantidade: number
  unidade: string
  precoUnitario: number
  precoTotal: number
  observacoes?: string | null
}

export interface Pedido {
  id: string
  numeroExibicao: string
  status: StatusPedido
  ehTeste: boolean
  valorTotal: number
  taxaEntrega: number
  clienteNome: string
  enderecoResumido?: string | null
  criadoNaOrigemEm: string
  recebidoEm: string
  /** Instante em que o prazo de confirmação do iFood expira, calculado no servidor. */
  prazoConfirmacaoAte: string
  itens: ItemDoPedido[]
}

// As colunas do painel. Concluído e Cancelado ficam fora: são destino final,
// não etapa de operação — a coluna existiria só para acumular histórico.
export const COLUNAS: StatusPedido[] = [
  'Recebido',
  'Confirmado',
  'EmPreparo',
  'Pronto',
  'Despachado',
]

export const TITULO_COLUNA: Record<StatusPedido, string> = {
  Recebido: 'Aguardando confirmação',
  Confirmado: 'Confirmados',
  EmPreparo: 'Em preparo',
  Pronto: 'Prontos',
  Despachado: 'Saiu para entrega',
  Concluido: 'Concluídos',
  Cancelado: 'Cancelados',
}

// A ação que leva o pedido ao próximo estado. O caminho do endpoint espelha a
// ação, não o status: quem decide a transição válida é o domínio no backend.
const PROXIMO: Partial<Record<StatusPedido, { destino: StatusPedido; acao: string; rotulo: string }>> = {
  Recebido: { destino: 'Confirmado', acao: 'confirmar', rotulo: 'Confirmar' },
  Confirmado: { destino: 'EmPreparo', acao: 'iniciar-preparo', rotulo: 'Iniciar preparo' },
  EmPreparo: { destino: 'Pronto', acao: 'pronto', rotulo: 'Marcar pronto' },
  Pronto: { destino: 'Despachado', acao: 'despachar', rotulo: 'Despachar' },
}

export function proximoPasso(status: StatusPedido) {
  return PROXIMO[status] ?? null
}

// O estado só anda para frente (regra da máquina de estados do backend: um
// evento atrasado nunca retrocede o pedido). A interface impede o movimento
// inválido antes de a requisição sair.
export function podeMoverPara(origem: StatusPedido, destino: StatusPedido): boolean {
  return proximoPasso(origem)?.destino === destino
}

export function acaoPara(origem: StatusPedido, destino: StatusPedido): string | null {
  const passo = proximoPasso(origem)

  return passo && passo.destino === destino ? passo.acao : null
}
