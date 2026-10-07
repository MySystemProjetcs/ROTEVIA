export type StatusPedido =
  | 'Recebido'
  | 'Confirmado'
  | 'EmPreparo'
  | 'Pronto'
  | 'Despachado'
  | 'Aceito'
  | 'EmRota'
  | 'Chegou'
  | 'Cobrar'
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
  itens: ItemDoPedido[]
  entregadorId?: string | null
  entregadorNome?: string | null
  /** Texto pronto para a tela: "Crédito Visa", "PIX", "Dinheiro". */
  pagamentoDescricao: string
  /** Maior que zero = o motoboy ainda precisa receber do cliente. */
  pagamentoValorACobrar: number
  /** Só vem preenchido na listagem do motoboy — ele pode atender mais de uma loja. */
  nomeLoja?: string | null
  /** De onde o pedido veio. "NoventaENove" existe no tipo porque a integração
   *  está só pausada, não removida — o backend ainda não emite esse valor. */
  origem: 'Interno' | 'IFood' | 'NoventaENove'
  /** Entrega própria do iFood exige que o cliente informe um código de 4
   *  dígitos na porta antes de finalizar — sem isso o iFood não confirma a
   *  entrega, e o cliente pode contestar como "não entregue". */
  exigeCodigoDeEntrega: boolean
  codigoConfirmadoEm?: string | null
  /** Pedidos casados: id da corrida (lote) e a posição da parada na rota
   *  (1-based). Nulos = entrega solo. A tela do motoboy agrupa por lote e
   *  ordena por ordemNaRota. */
  loteEntregaId?: string | null
  ordemNaRota?: number | null
}

// As colunas do painel do dono. Concluído e Cancelado ficam fora: são destino
// final, não etapa de operação. A partir de "Despachado" quem avança é o
// motoboy — o dono só acompanha o texto/etiqueta do card, não tem coluna nova
// pra cada passo dele.
// Uma coluna do quadro do dono. Deixou de ser "um status = uma coluna" porque
// a entrega tem quatro status (aceito, a caminho, no local, cobrando) que para
// quem está na cozinha são um só: o pedido saiu com o motoboy.
export interface ColunaDoQuadro {
  id: string
  titulo: string
  /** Pedidos nestes status aparecem aqui. */
  status: StatusPedido[]
  /** Status de onde a coluna tira sua cor. */
  cor: StatusPedido
  /** Destino ao arrastar um cartão para cá. Sem isto, a coluna não recebe. */
  destino?: StatusPedido
  /** Rola dentro de si em vez de esticar a página. */
  rolavel?: boolean
}

export const COLUNAS: ColunaDoQuadro[] = [
  { id: 'recebido', titulo: 'Aguardando', status: ['Recebido'], cor: 'Recebido', destino: 'Recebido' },
  { id: 'confirmado', titulo: 'Confirmados', status: ['Confirmado'], cor: 'Confirmado', destino: 'Confirmado' },
  { id: 'preparo', titulo: 'Em Preparo', status: ['EmPreparo'], cor: 'EmPreparo', destino: 'EmPreparo' },
  { id: 'pronto', titulo: 'Prontos', status: ['Pronto'], cor: 'Pronto', destino: 'Pronto' },
  { id: 'despachado', titulo: 'Aguard. Aceite', status: ['Despachado'], cor: 'Despachado', destino: 'Despachado' },
  // Sem `destino`: quem move o pedido daqui para frente é o motoboy, pelo app
  // dele. Arrastar um cartão para cá pela cozinha seria mentir sobre onde a
  // moto está.
  {
    id: 'em-rota',
    titulo: 'Em Rota',
    status: ['Aceito', 'EmRota', 'Chegou', 'Cobrar'],
    cor: 'EmRota',
  },
  // Rolável: a coluna só cresce ao longo do dia, e sem teto ela esticaria a
  // página inteira enquanto as outras ficam vazias no rodapé.
  {
    id: 'finalizados',
    titulo: 'Finalizados',
    status: ['Concluido'],
    cor: 'Concluido',
    rolavel: true,
  },
]

// Entrega em curso (aguardando aceite incluso): é o que separa "Em Entrega"
// de "Disponível" — no painel do dono, na página de motoboys e no toggle do
// motoboy. Um lugar só, pra ninguém divergir a definição.
export const STATUS_EM_ENTREGA: StatusPedido[] = [
  'Despachado',
  'Aceito',
  'EmRota',
  'Chegou',
  'Cobrar',
]

// Ordem das etapas do motoboy. Vira lista, não colunas: o painel dele é de uma
// coluna só. "Cobrar" fica no meio porque só existe em pedido com pendência —
// pedido pago pula de Chegou direto para Concluido.
export const COLUNAS_ENTREGADOR: StatusPedido[] = [
  'Despachado',
  'Aceito',
  'EmRota',
  'Chegou',
  'Cobrar',
]

export const TITULO_COLUNA: Record<StatusPedido, string> = {
  Recebido: 'Aguardando',
  Confirmado: 'Confirmados',
  EmPreparo: 'Em Preparo',
  Pronto: 'Prontos',
  Despachado: 'Aguard. Aceite',
  Aceito: 'Aceito',
  EmRota: 'A caminho',
  Chegou: 'No local',
  Cobrar: 'Cobrando',
  Concluido: 'Concluídos',
  Cancelado: 'Cancelados',
}

export interface PassoPedido {
  destino: StatusPedido
  acao: string
  rotulo: string
}

// Recebe o pedido inteiro, não só o status: depois de "Cheguei no local" o
// próximo passo depende de haver valor a cobrar — pedido pago vai direto para
// Finalizar, pedido com pendência passa por Cobrar antes.
export type ObterProximoPasso = (pedido: Pedido) => PassoPedido | null

// Precisa cobrar do cliente na entrega.
export function temValorACobrar(pedido: Pedido): boolean {
  return pedido.pagamentoValorACobrar > 0
}

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
  // Chegou não está aqui: o passo seguinte depende do pagamento, e quem decide
  // é a função abaixo.
  Cobrar: { destino: 'Concluido', acao: 'finalizar', rotulo: 'Finalizar entrega' },
}

const COBRAR: PassoPedido = { destino: 'Cobrar', acao: 'cobrar', rotulo: 'Cobrar do cliente' }
const FINALIZAR: PassoPedido = { destino: 'Concluido', acao: 'finalizar', rotulo: 'Finalizar entrega' }

export function proximoPasso(pedido: Pedido) {
  return PROXIMO[pedido.status] ?? null
}

// "Chegou" e "Cobrar" são as duas etapas de onde se finaliza — o código, se
// exigido, precisa ser confirmado em qualquer uma delas antes do botão
// "Finalizar entrega" fazer sentido. O backend recusa Finalizar sem isso
// (Pedido.ConcluirPeloEntregador); esta função é o que impede a tela de nem
// oferecer o botão que o servidor já sabe que vai rejeitar.
export function precisaConfirmarCodigoDeEntrega(pedido: Pedido): boolean {
  return (
    (pedido.status === 'Chegou' || pedido.status === 'Cobrar') &&
    pedido.exigeCodigoDeEntrega &&
    !pedido.codigoConfirmadoEm
  )
}

// O estado só anda para frente (regra da máquina de estados do backend: um
// evento atrasado nunca retrocede o pedido). A interface impede o movimento
// inválido antes de a requisição sair.
export function podeMoverPara(pedido: Pedido, destino: StatusPedido): boolean {
  return proximoPasso(pedido)?.destino === destino
}

export function acaoPara(pedido: Pedido, destino: StatusPedido): string | null {
  const passo = proximoPasso(pedido)

  return passo && passo.destino === destino ? passo.acao : null
}

// Mesmo trio de funções, lado do motoboy — espelha PROXIMO_ENTREGADOR.
export function proximoPassoEntregador(pedido: Pedido) {
  // A bifurcação do fluxo: em "Chegou", quem tem pendência cobra antes de
  // finalizar; quem já pagou online encerra direto. Pedir dinheiro a quem já
  // pagou seria cobrar duas vezes.
  if (pedido.status === 'Chegou') {
    return temValorACobrar(pedido) ? COBRAR : FINALIZAR
  }

  return PROXIMO_ENTREGADOR[pedido.status] ?? null
}

export function podeMoverParaEntregador(pedido: Pedido, destino: StatusPedido): boolean {
  return proximoPassoEntregador(pedido)?.destino === destino
}

export function acaoParaEntregador(pedido: Pedido, destino: StatusPedido): string | null {
  const passo = proximoPassoEntregador(pedido)

  return passo && passo.destino === destino ? passo.acao : null
}
