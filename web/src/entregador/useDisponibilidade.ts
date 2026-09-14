import { useCallback, useEffect, useState } from 'react'
import { api, ErroDaApi } from '@/lib/api'

interface RespostaDisponibilidade {
  disponivel: boolean
}

// Interruptor Online/Offline do motoboy. Otimista com rollback, no mesmo
// espírito do mover() dos pedidos: o motoboy não pode esperar a rede para
// ver o próprio status mudar.
export function useDisponibilidade() {
  const [disponivel, setDisponivel] = useState<boolean | null>(null)
  const [erro, setErro] = useState<string | null>(null)

  useEffect(() => {
    let ativo = true

    api
      .get<RespostaDisponibilidade>('/entregador/disponibilidade')
      .then((resposta) => {
        if (ativo) setDisponivel(resposta.disponivel)
      })
      .catch(() => {
        if (ativo) setErro('Não foi possível carregar a disponibilidade.')
      })

    return () => {
      ativo = false
    }
  }, [])

  const definir = useCallback(async (valor: boolean) => {
    const anterior = disponivel
    setDisponivel(valor)
    setErro(null)

    try {
      const resposta = await api.post<RespostaDisponibilidade>('/entregador/disponibilidade', {
        disponivel: valor,
      })
      setDisponivel(resposta.disponivel)
    } catch (e) {
      setDisponivel(anterior)
      setErro(e instanceof ErroDaApi ? e.message : 'Não foi possível trocar a disponibilidade.')
    }
  }, [disponivel])

  return { disponivel, erro, definir }
}
