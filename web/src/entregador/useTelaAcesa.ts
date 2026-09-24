import { useEffect, useState } from 'react'

export type EstadoTelaAcesa = 'inativo' | 'ativa' | 'indisponivel' | 'perdida' | 'erro'

export interface ResultadoTelaAcesa {
  estado: EstadoTelaAcesa
  abaVisivel: boolean
}

// Mantém a tela do celular acesa enquanto o motoboy está em turno. O Wake Lock
// é uma proteção de execução, não uma garantia de GPS: se falhar, o rastreamento
// continua e a interface informa a condição degradada.
export function useTelaAcesa(ativo: boolean): ResultadoTelaAcesa {
  const [estado, setEstado] = useState<EstadoTelaAcesa>('inativo')
  const [abaVisivel, setAbaVisivel] = useState(true)

  useEffect(() => {
    if (!ativo) {
      setEstado('inativo')
      setAbaVisivel(true)
      return
    }

    if (typeof navigator === 'undefined' || !('wakeLock' in navigator)) {
      setEstado('indisponivel')
      return
    }

    let travaAtual: WakeLockSentinel | null = null
    let cancelado = false

    function aoLiberarTrava() {
      if (!cancelado) {
        travaAtual = null
        setEstado('perdida')
      }
    }

    async function travar() {
      if (cancelado || document.visibilityState !== 'visible') return

      try {
        travaAtual?.removeEventListener('release', aoLiberarTrava)
        travaAtual = await navigator.wakeLock.request('screen')
        travaAtual.addEventListener('release', aoLiberarTrava)
        setEstado('ativa')
      } catch {
        if (!cancelado) setEstado('erro')
      }
    }

    function aoMudarVisibilidade() {
      const visivel = document.visibilityState === 'visible'
      setAbaVisivel(visivel)
      if (visivel && !cancelado) void travar()
    }

    setAbaVisivel(document.visibilityState === 'visible')
    document.addEventListener('visibilitychange', aoMudarVisibilidade)
    void travar()

    return () => {
      cancelado = true
      document.removeEventListener('visibilitychange', aoMudarVisibilidade)
      travaAtual?.removeEventListener('release', aoLiberarTrava)
      void travaAtual?.release()
    }
  }, [ativo])

  return { estado, abaVisivel }
}
