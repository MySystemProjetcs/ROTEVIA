export type StatusPedido =
  | 'Recebido'
  | 'Confirmado'
  | 'EmPreparo'
  | 'Pronto'
  | 'Despachado'
  | 'Aceito'
  | 'EmRota'
  | 'Chegou'
  | 'Concluido'
  | 'Cancelado'

export type PapelUsuario = 'AdministradorSistema' | 'DonoRestaurante' | 'Entregador'

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
  /** Nulas quando o destino não é conhecido — inclusive no sandbox do iFood,
   *  que manda 0,0 e o backend traduz para nulo. */
  enderecoLatitude?: number | null
  enderecoLongitude?: number | null
  criadoNaOrigemEm: string
  recebidoEm: string
  /** Instante em que o prazo de confirmação do iFood expira, calculado no servidor. */
  prazoConfirmacaoAte: string
  itens: ItemDoPedido[]
  entregadorId?: string | null
  entregadorNome?: string | null
  /** Só vem preenchido na listagem do motoboy — ele pode atender mais de uma loja. */
  nomeLoja?: string | null
}

// As colunas do painel do dono. Concluído e Cancelado ficam fora: são destino
// final, não etapa de operação. A partir de "Despachado" quem avança é o
// motoboy — o dono só acompanha o texto/etiqueta do card, não tem coluna nova
// pra cada passo dele.
export const COLUNAS: StatusPedido[] = [
  'Recebido',
  'Confirmado',
  'EmPreparo',
  'Pronto',
  'Despachado',
]

// Entrega em curso (aguardando aceite incluso): é o que separa "Em Entrega"
// de "Disponível" — no painel do dono, na página de motoboys e no toggle do
// motoboy. Um lugar só, pra ninguém divergir a definição.
export const STATUS_EM_ENTREGA: StatusPedido[] = ['Despachado', 'Aceito', 'EmRota', 'Chegou']

// As colunas do painel do motoboy (mesmo sistema, papel diferente).
export const COLUNAS_ENTREGADOR: StatusPedido[] = ['Despachado', 'Aceito', 'EmRota', 'Chegou']

export const TITULO_COLUNA: Record<StatusPedido, string> = {
  Recebido: 'Aguardando',
  Confirmado: 'Confirmados',
  EmPreparo: 'Em Preparo',
  Pronto: 'Prontos',
  Despachado: 'Aguard. Aceite',
  Aceito: 'Aceito',
  EmRota: 'A caminho',
  Chegou: 'No local',
  Concluido: 'Concluídos',
  Cancelado: 'Cancelados',
}

export interface PassoPedido {
  destino: StatusPedido
  acao: string
  rotulo: string
}

// Assinatura comum de proximoPasso/proximoPassoEntregador — usada pelo Kanban
// pra aceitar o mapa de qualquer um dos dois papéis sem duplicar o tipo.
export type ObterProximoPasso = (status: StatusPedido) => PassoPedido | null

// A ação que leva o pedido ao próximo estado — lado do dono. O caminho do
// endpoint espelha a ação, não o status: quem decide a transição válida é o
// domínio no backend.
const PROXIMO: Partial<Record<StatusPedido, PassoPedido>> = {
  Recebido: { destino: 'Confirmado', acao: 'confirmar', rotulo: 'Confirmar' },
  Confirmado: { destino: 'EmPreparo', acao: 'iniciar-preparo', rotulo: 'Iniciar preparo' },
  EmPreparo: { destino: 'Pronto', acao: 'pronto', rotulo: 'Marcar pronto' },
  Pronto: { destino: 'Despachado', acao: 'despachar', rotulo: 'Despachar' },
}

// Idem, lado do motoboy — endpoints diferentes (/api/entregador/...), por
// isso um mapa separado em vez de estender o de cima.
export const PROXIMO_ENTREGADOR: Partial<Record<StatusPedido, PassoPedido>> = {
  Despachado: { destino: 'Aceito', acao: 'aceitar', rotulo: 'Aceitar' },
  Aceito: { destino: 'EmRota', acao: 'sair-para-entrega', rotulo: 'Sair para entrega' },
  EmRota: { destino: 'Chegou', acao: 'cheguei', rotulo: 'Cheguei no local' },
  Chegou: { destino: 'Concluido', acao: 'finalizar', rotulo: 'Finalizar entrega' },
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

// Mesmo trio de funções, lado do motoboy — espelha PROXIMO_ENTREGADOR.
export function proximoPassoEntregador(status: StatusPedido) {
  return PROXIMO_ENTREGADOR[status] ?? null
}

export function podeMoverParaEntregador(origem: StatusPedido, destino: StatusPedido): boolean {
  return proximoPassoEntregador(origem)?.destino === destino
}

export function acaoParaEntregador(origem: StatusPedido, destino: StatusPedido): string | null {
  const passo = proximoPassoEntregador(origem)

  return passo && passo.destino === destino ? passo.acao : null
}
