import * as signalR from '@microsoft/signalr'
import { useEffect } from 'react'
import type { Comanda } from '@/lib/impressao'
import { imprimirComanda } from '@/lib/impressao'
import { lerToken } from '@/lib/sessao'

// Ouve "PedidoParaImprimir" no mesmo Hub usado pelo painel. Dispara a
// impressão (PDF em dev, ESC/POS em prod) a cada pedido novo persistido pelo
// worker. Falha silenciosa — se o Hub não conectar, nada é impresso, e o
// lojista pode reimprimir manualmente pelo cartão quando essa ação existir.
export function useImpressaoAutomatica(ativo: boolean) {
  useEffect(() => {
    if (!ativo) return

    const conexao = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/rastreio', {
        accessTokenFactory: () => lerToken() ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    conexao.on('PedidoParaImprimir', (comanda: Comanda) => {
      try {
        imprimirComanda(comanda)
      } catch (e) {
        // eslint-disable-next-line no-console
        console.error('[impressão] falha ao imprimir', e)
      }
    })

    const iniciar = async () => {
      try {
        await conexao.start()
        await conexao.invoke('EntrarNoGrupo')
      } catch {
        // Hub indisponível: nada a imprimir automaticamente nesta sessão.
      }
    }

    void iniciar()

    return () => {
      void conexao
        .invoke('SairDoGrupo')
        .catch(() => {})
        .finally(() => conexao.stop())
    }
  }, [ativo])
}
