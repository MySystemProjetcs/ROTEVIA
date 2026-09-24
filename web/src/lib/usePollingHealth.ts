import { useEffect, useState } from 'react'
import { api } from './api'

interface PollingHealth {
  online: boolean
  ultimoPolling: string | null
}

// Consulta o health check do worker a cada 15s para mostrar o status no
// rodapé da sidebar. Falha silenciosa — não quebra a UI se o worker estiver
// reiniciando.
export function usePollingHealth(): PollingHealth {
  const [estado, setEstado] = useState<PollingHealth>({ online: false, ultimoPolling: null })

  useEffect(() => {
    async function checar() {
      try {
        const res = await api.get<{ status: string; entries?: Record<string, { data?: Record<string, unknown> }> }>('/health')
        const dados = res.entries?.['polling']?.data
        const segundos = typeof dados?.['segundosDesdeUltimoSucesso'] === 'number'
          ? (dados['segundosDesdeUltimoSucesso'] as number)
          : null

        setEstado({
          online: res.status === 'Healthy',
          ultimoPolling: segundos !== null ? `${segundos}s` : null,
        })
      } catch {
        setEstado({ online: false, ultimoPolling: null })
      }
    }

    void checar()
    const id = setInterval(checar, 15_000)
    return () => clearInterval(id)
  }, [])

  return estado
}
