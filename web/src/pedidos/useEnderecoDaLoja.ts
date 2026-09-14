import { useEffect, useState } from 'react'
import { api } from '@/lib/api'

export interface EnderecoDaLoja {
  resumo: string
  latitude: number
  longitude: number
}

// Ponto fixo do mapa. Busca uma vez por sessão: endereço de loja não muda
// enquanto o painel está aberto.
export function useEnderecoDaLoja(merchantId: string | null | undefined) {
  const [endereco, setEndereco] = useState<EnderecoDaLoja | null>(null)

  useEffect(() => {
    if (!merchantId) return

    let cancelado = false

    api
      .get<EnderecoDaLoja>(`/restaurantes/${merchantId}/endereco`)
      .then((dados) => {
        if (!cancelado) setEndereco(dados)
      })
      // 404 é o caso normal de loja sem endereço informado — o mapa apenas
      // não desenha o pin dela.
      .catch(() => {})

    return () => {
      cancelado = true
    }
  }, [merchantId])

  return endereco
}
