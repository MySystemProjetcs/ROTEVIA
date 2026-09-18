import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { Botao } from '@/components/Botao'
import { CampoSenha } from '@/components/CampoSenha'
import { Cartao, CartaoCorpo } from '@/components/Cartao'
import { Logo } from '@/components/Logo'
import { api, ErroDaApi } from '@/lib/api'

interface PreviaDoConvite {
  nomeEntregador: string
  nomeLoja: string
  valido: boolean
}

export function ConfirmarConvite() {
  const { linkId } = useParams<{ linkId: string }>()
  const [parametros] = useSearchParams()
  const token = parametros.get('token') ?? ''

  const [previa, setPrevia] = useState<PreviaDoConvite | null>(null)
  const [carregandoPrevia, setCarregandoPrevia] = useState(true)
  const [erroPrevia, setErroPrevia] = useState<string | null>(null)

  const [senha, setSenha] = useState('')
  const [confirmacao, setConfirmacao] = useState('')
  const [erroForm, setErroForm] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)
  const [concluido, setConcluido] = useState(false)

  useEffect(() => {
    if (!linkId) return

    api
      .get<PreviaDoConvite>(`/convites/entregador/${linkId}`)
      .then(setPrevia)
      .catch((e) => setErroPrevia(e instanceof ErroDaApi ? e.message : 'Não foi possível abrir o convite.'))
      .finally(() => setCarregandoPrevia(false))
  }, [linkId])

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault()
    setErroForm(null)

    if (senha !== confirmacao) {
      setErroForm('As senhas não são iguais.')
      return
    }

    setEnviando(true)
    try {
      await api.post(`/convites/entregador/${linkId}/confirmar`, { token, senha })
      setConcluido(true)
    } catch (e) {
      setErroForm(e instanceof ErroDaApi ? e.message : 'Não foi possível confirmar o convite.')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <main className="flex min-h-dvh items-center justify-center bg-superficie-alt p-4">
      <Cartao elevacao="elevada" className="w-full max-w-sm">
        <CartaoCorpo>
          <div className="flex flex-col items-center text-center">
            <Logo tamanho="grande" />
          </div>

          {carregandoPrevia ? (
            <p className="mt-6 text-center text-apoio text-texto-suave">Carregando convite...</p>
          ) : erroPrevia || !previa ? (
            <p className="mt-6 text-center text-apoio text-perigo">
              {erroPrevia ?? 'Convite não encontrado.'}
            </p>
          ) : !previa.valido ? (
            <p className="mt-6 text-center text-apoio text-perigo">
              Este convite expirou. Peça para {previa.nomeLoja} gerar um novo.
            </p>
          ) : concluido ? (
            <div className="mt-6 flex flex-col gap-4 text-center">
              <p className="text-apoio text-texto">Senha criada! Você já pode entrar na plataforma.</p>
              <Link to="/login">
                <Botao larguraTotal>Ir para o login</Botao>
              </Link>
            </div>
          ) : (
            <>
              <p className="mt-2 text-center text-apoio text-texto-suave">
                Olá, {previa.nomeEntregador}. Você foi convidado por {previa.nomeLoja} para ser
                entregador no ROTEVIA. Crie sua senha para continuar.
              </p>

              <form onSubmit={aoEnviar} className="mt-6 flex flex-col gap-4">
                <CampoSenha
                  rotulo="Nova senha"
                  autoComplete="new-password"
                  required
                  minLength={8}
                  value={senha}
                  onChange={(e) => setSenha(e.target.value)}
                />

                <CampoSenha
                  rotulo="Confirmar senha"
                  autoComplete="new-password"
                  required
                  value={confirmacao}
                  onChange={(e) => setConfirmacao(e.target.value)}
                  erro={erroForm ?? undefined}
                />

                <Botao type="submit" carregando={enviando} larguraTotal>
                  Criar senha e ativar conta
                </Botao>
              </form>
            </>
          )}
        </CartaoCorpo>
      </Cartao>
    </main>
  )
}
