import { useCallback, useEffect, useState } from 'react'
import { useSessao } from '@/auth/SessaoProvider'
import type { Entregador, EntregadorConvidado, NovoEntregador } from '@/dominio/entregador'
import { api, ErroDaApi } from '@/lib/api'
import { normalizarTelefoneBr } from '@/lib/telefone'

export function useMotoboys() {
  const { usuario } = useSessao()
  const merchantId = usuario?.merchantId ?? null

  const [entregadores, setEntregadores] = useState<Entregador[]>([])
  const [carregando, setCarregando] = useState(true)
  const [erro, setErro] = useState<string | null>(null)

  const recarregar = useCallback(async () => {
    if (!merchantId) return

    try {
      setEntregadores(await api.get<Entregador[]>(`/restaurantes/${merchantId}/entregadores`))
      setErro(null)
    } catch (e) {
      setErro(e instanceof ErroDaApi ? e.message : 'Não foi possível carregar os motoboys.')
    } finally {
      setCarregando(false)
    }
  }, [merchantId])

  useEffect(() => {
    void recarregar()
  }, [recarregar])

  const convidar = useCallback(
    async (dados: NovoEntregador): Promise<EntregadorConvidado> => {
      if (!merchantId) throw new Error('Sem loja vinculada.')

      const resultado = await api.post<EntregadorConvidado>(`/restaurantes/${merchantId}/entregadores`, {
        ...dados,
        telefone: normalizarTelefoneBr(dados.telefone),
      })
      await recarregar()
      return resultado
    },
    [merchantId, recarregar],
  )

  return { entregadores, carregando, erro, convidar }
}
