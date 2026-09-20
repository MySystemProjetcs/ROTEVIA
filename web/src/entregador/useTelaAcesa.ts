import { useEffect } from 'react'

// Mantém a tela do celular acesa enquanto o motoboy está em turno.
//
// Não é conforto: quando a tela apaga, o navegador congela a aba, o
// watchPosition para de disparar e a loja deixa de ver a moto andar. O Wake
// Lock é a única forma de evitar isso sem sair do navegador.
//
// API nativa, sem pacote. Só existe em contexto seguro (HTTPS) e se perde
// sozinho quando a aba vai para segundo plano — daí o reatar no
// visibilitychange, que é o caso de voltar ao app depois de olhar o WhatsApp.
export function useTelaAcesa(ativo: boolean): void {
  useEffect(() => {
    if (!ativo) return
    if (typeof navigator === 'undefined' || !('wakeLock' in navigator)) return

    let travaAtual: WakeLockSentinel | null = null
    let cancelado = false

    async function travar() {
      try {
        travaAtual = await navigator.wakeLock.request('screen')
      } catch {
        // Bateria baixa, aba em segundo plano ou navegador sem suporte: o app
        // segue funcionando, só sem garantia de tela acesa.
      }
    }

    function aoMudarVisibilidade() {
      if (document.visibilityState === 'visible' && !cancelado) void travar()
    }

    void travar()
    document.addEventListener('visibilitychange', aoMudarVisibilidade)

    return () => {
      cancelado = true
      document.removeEventListener('visibilitychange', aoMudarVisibilidade)
      void travaAtual?.release()
    }
  }, [ativo])
}
