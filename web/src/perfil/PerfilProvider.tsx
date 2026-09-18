import { useCallback, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { api, ErroDaApi } from '@/lib/api'
import { ContextoDoPerfil } from '@/perfil/contexto'
import type { Perfil } from '@/perfil/contexto'

export function PerfilProvider({ children }: { children: ReactNode }) {
  const [perfil, setPerfil] = useState<Perfil | null>(null)
  const [carregando, setCarregando] = useState(true)
  const [erro, setErro] = useState<string | null>(null)

  useEffect(() => {
    let cancelado = false

    api
      .get<Perfil>('/perfil')
      .then((dados) => {
        if (!cancelado) setPerfil(dados)
      })
      .catch(() => {
        if (!cancelado) setErro('Não foi possível carregar o perfil.')
      })
      .finally(() => {
        if (!cancelado) setCarregando(false)
      })

    return () => {
      cancelado = true
    }
  }, [])

  // Atualiza local depois do 204: o servidor não devolve corpo, e recarregar o
  // perfil inteiro para ver a foto que acabamos de enviar seria ida e volta à
  // toa. Como o estado é o da sessão, a barra lateral muda junto.
  const enviarFoto = useCallback(async (fotoBase64: string) => {
    try {
      await api.put('/perfil/foto', { fotoBase64 })
      setPerfil((atual) => (atual ? { ...atual, fotoBase64 } : atual))
      setErro(null)
    } catch (e) {
      setErro(e instanceof ErroDaApi ? e.message : 'Não foi possível salvar a foto.')
      throw e
    }
  }, [])

  const valor = useMemo(
    () => ({ perfil, carregando, erro, enviarFoto }),
    [perfil, carregando, erro, enviarFoto],
  )

  return <ContextoDoPerfil.Provider value={valor}>{children}</ContextoDoPerfil.Provider>
}
