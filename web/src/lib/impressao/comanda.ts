// Espelho do PedidoReceiptSnapshot do backend. Vem tanto do push SignalR
// "PedidoParaImprimir" (primeira impressão) quanto do GET /pedidos/:id/comanda
// (reimpressão). Mesmo formato, mesmo schemaVersion.

export interface Comanda {
  schemaVersion: number
  pedidoId: string
  numeroExibicao: string
  ehTeste: boolean
  origem: string
  idExterno: string
  merchantId: string
  criadoEm: string
  recebidoEm: string
  cliente: ComandaCliente
  entrega: ComandaEntrega | null
  itens: ComandaItem[]
  pagamento: ComandaPagamento
  valorTotal: number
  taxaEntrega: number
  codigoDeEntrega: string | null
  exigeCodigoDeEntrega: boolean
}

export interface ComandaCliente {
  nome: string
  telefone: string | null
  localizador: string | null
}

export interface ComandaEntrega {
  logradouro: string | null
  numero: string | null
  complemento: string | null
  bairro: string | null
  cidade: string | null
  estado: string | null
  cep: string | null
  referencia: string | null
}

export interface ComandaItem {
  indice: number
  nome: string
  quantidade: number
  unidade: string | null
  precoUnitario: number
  precoTotal: number
  observacoes: string | null
}

export interface ComandaPagamento {
  descricao: string | null
  valorACobrar: number | null
  valorJaPago: number | null
}

export interface OpcoesImpressao {
  // true marca a comanda como REIMPRESSÃO: banner no topo e, no futuro,
  // omissão do QR code oficial do iFood (a primeira impressão deve preservar
  // o QR quando a integração vier a extraí-lo; reimpressão nunca).
  reimpressao?: boolean
}

export function enderecoFormatado(e: ComandaEntrega | null): string {
  if (!e) return ''
  const linha1 = [e.logradouro, e.numero].filter(Boolean).join(', ')
  const linha2 = [e.bairro, e.cidade && e.estado ? `${e.cidade}/${e.estado}` : e.cidade ?? e.estado]
    .filter(Boolean)
    .join(' - ')
  return [linha1, e.complemento, linha2, e.cep, e.referencia].filter(Boolean).join('\n')
}

export function moeda(valor: number): string {
  return valor.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
}
