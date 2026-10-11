import type { Comanda, OpcoesImpressao } from './comanda'
import { imprimirComandaPdf } from './clientePdf'
import { imprimirComandaEscPos } from './clienteEscPos'

export type { Comanda, ComandaItem, ComandaCliente, ComandaEntrega, ComandaPagamento, OpcoesImpressao } from './comanda'

// Decisão simples: Vite expõe import.meta.env.MODE === 'production' no build
// de produção. Em development/sandbox cai no PDF, qualquer outro ambiente
// (incluindo preview) manda para a impressora física.
const modoDeDesenvolvimento = import.meta.env.MODE !== 'production'

export function imprimirComanda(comanda: Comanda, opcoes: OpcoesImpressao = {}): void {
  if (modoDeDesenvolvimento) {
    imprimirComandaPdf(comanda, opcoes)
    return
  }
  imprimirComandaEscPos(comanda, opcoes)
}

export { imprimirComandaPdf, imprimirComandaEscPos }
