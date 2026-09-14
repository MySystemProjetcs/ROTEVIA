import * as signalR from '@microsoft/signalr'
import { useCallback, useEffect, useState } from 'react'
import { api, ErroDaApi } from '@/lib/api'
import { lerToken } from '@/lib/sessao'

export interface ResumoDashboard {
  motoboysOnline: number
  motoboysEmEntrega: number
  qtdPedidosHoje: number
  receitaHoje: number
  taxaPorEntrega: number
  qtdDeTeste: number
}

// Poll de segurança a cada 10s: o push cobre as mutações da API, mas a
// ingestão do worker (pedido novo do iFood) chega pelo polling — sem o
// intervalo, o número novo demoraria até a próxima ação de alguém.
const INTERVALO_MS = 10_000

export function useResumoDashboard(merchantId: string | null | undefined) {
  const [resumo, setResumo] = useState<ResumoDashboard | null>(null)
  const [erro, setErro] = useState<string | null>(null)

  const recarregar = useCallback(async () => {
    if (!merchantId) return

    try {
      setResumo(await api.get<ResumoDashboard>(`/restaurantes/${merchantId}/dashboard/resumo`))
      setErro(null)
    } catch (e) {
      setErro(e instanceof ErroDaApi ? e.message : 'Não foi possível carregar o resumo.')
    }
  }, [merchantId])

  useEffect(() => {
    void recarregar()
    const id = setInterval(() => void recarregar(), INTERVALO_MS)

    return () => clearInterval(id)
  }, [recarregar])

  // Mesmo Hub do rastreio, outro método: o servidor avisa "ResumoAtualizado"
  // no grupo da loja a cada mutação — a tela antecipa sem esperar o poll.
  useEffect(() => {
    if (!merchantId) return

    const conexao = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/rastreio', {
        accessTokenFactory: () => lerToken() ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    conexao.on('ResumoAtualizado', () => {
      void recarregar()
    })

    const iniciar = async () => {
      try {
        await conexao.start()
        // Sem argumento: o servidor resolve a loja pelo token.
        await conexao.invoke('EntrarNoGrupo')
      } catch {
        // Silencioso: o poll de 10s cobre a ausência do push.
      }
    }

    void iniciar()

    return () => {
      void conexao
        .invoke('SairDoGrupo')
        .catch(() => {})
        .finally(() => conexao.stop())
    }
  }, [merchantId, recarregar])

  return { resumo, erro, recarregar }
}
