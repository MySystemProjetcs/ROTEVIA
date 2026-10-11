import type { Comanda, OpcoesImpressao } from './comanda'

// Stub: em produção, aqui entra o envio ESC/POS para a impressora física —
// via QZ Tray (bridge WebSocket local) ou plugin nativo do Capacitor.
// Mantido como no-op para não quebrar o fluxo até a integração ser escolhida
// e implementada. O cliente PDF já cobre dev/sandbox integralmente.
//
// A opção `reimpressao` chega aqui para o futuro adaptador adicionar o banner
// REIMPRESSÃO no ESC/POS e omitir o QR code oficial do iFood — nunca gerar
// substituto (plano §3).
export function imprimirComandaEscPos(comanda: Comanda, opcoes: OpcoesImpressao = {}): void {
  // eslint-disable-next-line no-console
  console.warn(
    '[impressão] cliente ESC/POS ainda não implementado — pedido %s%s não foi impresso fisicamente.',
    comanda.numeroExibicao,
    opcoes.reimpressao ? ' (reimpressão)' : '',
  )
}
