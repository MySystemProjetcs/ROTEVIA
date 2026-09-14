import { useCallback, useEffect, useState } from 'react'
import { useSessao } from '@/auth/SessaoProvider'
import { api, ErroDaApi } from '@/lib/api'

export type StatusConexaoWhatsApp = 'Desconectado' | 'AguardandoLeituraDoQr' | 'Conectado' | 'Erro'

export interface StatusWhatsApp {
  status: StatusConexaoWhatsApp
  qrCodeBase64: string | null
  telefone: string | null
  erro: string | null
}

// QR expira em segundos no WhatsApp Web de verdade — precisa de um intervalo
// bem mais curto que o polling de pedidos (10s) pra não mostrar um QR morto.
const INTERVALO_MS = 3_000

export function useConexaoWhatsapp() {
  const { usuario } = useSessao()
  const merchantId = usuario?.merchantId ?? null

  const [dados, setDados] = useState<StatusWhatsApp | null>(null)
  const [carregando, setCarregando] = useState(true)
  const [erro, setErro] = useState<string | null>(null)
  const [conectando, setConectando] = useState(false)

  const consultarStatus = useCallback(async () => {
    if (!merchantId) return

    try {
      setDados(await api.get<StatusWhatsApp>(`/restaurantes/${merchantId}/whatsapp/status`))
      setErro(null)
    } catch (e) {
      setErro(e instanceof ErroDaApi ? e.message : 'Não foi possível consultar o WhatsApp.')
    } finally {
      setCarregando(false)
    }
  }, [merchantId])

  useEffect(() => {
    void consultarStatus()
    const id = setInterval(() => void consultarStatus(), INTERVALO_MS)
    return () => clearInterval(id)
  }, [consultarStatus])

  const conectar = useCallback(async () => {
    if (!merchantId) return

    setConectando(true)
    try {
      setDados(await api.post<StatusWhatsApp>(`/restaurantes/${merchantId}/whatsapp/conectar`))
      setErro(null)
    } catch (e) {
      setErro(e instanceof ErroDaApi ? e.message : 'Não foi possível conectar o WhatsApp.')
    } finally {
      setConectando(false)
    }
  }, [merchantId])

  const desconectar = useCallback(async () => {
    if (!merchantId) return

    try {
      await api.post(`/restaurantes/${merchantId}/whatsapp/desconectar`)
      await consultarStatus()
    } catch (e) {
      setErro(e instanceof ErroDaApi ? e.message : 'Não foi possível desconectar o WhatsApp.')
    }
  }, [merchantId, consultarStatus])

  return { dados, carregando, erro, conectando, conectar, desconectar }
}
