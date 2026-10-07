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

  // Endereçado pelo linkId (o vínculo com esta loja), não pelo courierId: é o
  // vínculo que prova que o motoboy é desta loja.
  const atualizar = useCallback(
    async (
      linkId: string,
      dados: { nome: string; telefone: string; modeloDaMoto: string; placa: string },
    ): Promise<string | null> => {
      if (!merchantId) return 'Sem loja vinculada.'

      try {
        await api.put(`/restaurantes/${merchantId}/entregadores/${linkId}`, {
          ...dados,
          telefone: normalizarTelefoneBr(dados.telefone),
        })
        await recarregar()
        return null
      } catch (e) {
        return e instanceof ErroDaApi ? e.message : 'Não foi possível salvar o cadastro.'
      }
    },
    [merchantId, recarregar],
  )

  // Vale para os dois casos: revoga o convite pendente ou desvincula o motoboy
  // ativo. O cadastro global dele não é apagado — some só desta loja.
  const remover = useCallback(
    async (linkId: string): Promise<string | null> => {
      if (!merchantId) return 'Sem loja vinculada.'

      try {
        await api.del(`/restaurantes/${merchantId}/entregadores/${linkId}`)
        await recarregar()
        return null
      } catch (e) {
        return e instanceof ErroDaApi ? e.message : 'Não foi possível remover o motoboy.'
      }
    },
    [merchantId, recarregar],
  )

  return { entregadores, carregando, erro, convidar, atualizar, remover }
}
