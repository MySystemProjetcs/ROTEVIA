import { useCallback, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { useSessao } from '@/auth/SessaoProvider'
import { api, ErroDaApi } from '@/lib/api'
import { ContextoDoPerfil } from '@/perfil/contexto'
import type { NovoEnderecoDaLoja, Perfil } from '@/perfil/contexto'

export function PerfilProvider({ children }: { children: ReactNode }) {
  const { usuario } = useSessao()
  const merchantId = usuario?.merchantId ?? null
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

  // Relê do servidor depois de editar. O nome da loja e o endereço voltam
  // formatados pelo backend (o endereço, inclusive, é montado a partir do CEP),
  // então adivinhar o texto localmente daria divergência na primeira troca.
  const recarregar = useCallback(async () => {
    const dados = await api.get<Perfil>('/perfil')
    setPerfil(dados)
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

  const alterarEmail = useCallback(
    async (email: string): Promise<string | null> => {
      try {
        await api.put('/perfil/email', { email })
        await recarregar()
        return null
      } catch (e) {
        return e instanceof ErroDaApi ? e.message : 'Não foi possível salvar o e-mail.'
      }
    },
    [recarregar],
  )

  const alterarNomeDaLoja = useCallback(
    async (nome: string): Promise<string | null> => {
      if (!merchantId) return 'Esta conta não está vinculada a uma loja.'

      try {
        await api.put(`/restaurantes/${merchantId}/nome`, { nome })
        await recarregar()
        return null
      } catch (e) {
        return e instanceof ErroDaApi ? e.message : 'Não foi possível salvar o nome da loja.'
      }
    },
    [merchantId, recarregar],
  )

  const alterarEndereco = useCallback(
    async (novo: NovoEnderecoDaLoja): Promise<string | null> => {
      if (!merchantId) return 'Esta conta não está vinculada a uma loja.'

      try {
        await api.put(`/restaurantes/${merchantId}/endereco`, {
          cep: novo.cep,
          numero: novo.numero,
          complemento: novo.complemento || null,
          referencia: null,
          latitude: novo.latitude ?? null,
          longitude: novo.longitude ?? null,
        })
        await recarregar()
        return null
      } catch (e) {
        return e instanceof ErroDaApi ? e.message : 'Não foi possível salvar o endereço.'
      }
    },
    [merchantId, recarregar],
  )

  const valor = useMemo(
    () => ({
      perfil,
      carregando,
      erro,
      enviarFoto,
      alterarEmail,
      alterarNomeDaLoja,
      alterarEndereco,
    }),
    [perfil, carregando, erro, enviarFoto, alterarEmail, alterarNomeDaLoja, alterarEndereco],
  )

  return <ContextoDoPerfil.Provider value={valor}>{children}</ContextoDoPerfil.Provider>
}
