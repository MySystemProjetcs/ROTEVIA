import { useState } from 'react'
import type { FormEvent } from 'react'
import { esquecerEmail, lerEmailSalvo, pedirParaNavegadorSalvar, salvarEmail } from '@/auth/acessoSalvo'
import { useSessao } from '@/auth/SessaoProvider'
import { Botao } from '@/components/Botao'
import { Campo } from '@/components/Campo'
import { CampoSenha } from '@/components/CampoSenha'
import { Cartao, CartaoCorpo } from '@/components/Cartao'
import { Logo } from '@/components/Logo'
import { Switch } from '@/components/Switch'
import { ErroDaApi } from '@/lib/api'

export function Login() {
  const { entrar } = useSessao()
  // Inicializador lazy: o e-mail lembrado entra antes da primeira renderização,
  // sem o campo piscar vazio.
  const [email, setEmail] = useState(lerEmailSalvo)
  const [senha, setSenha] = useState('')
  // Marcado quando já há e-mail lembrado: quem salvou uma vez quer continuar
  // salvando, e desmarcar é o que precisa ser explícito.
  const [salvarAcesso, setSalvarAcesso] = useState(() => lerEmailSalvo() !== '')
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault()
    setErro(null)
    setEnviando(true)

    try {
      await entrar(email, senha)

      // Só depois do login dar certo: salvar credencial recusada pelo servidor
      // deixaria o cofre do navegador com uma senha que não funciona.
      if (salvarAcesso) {
        salvarEmail(email)
        await pedirParaNavegadorSalvar(email, senha)
      } else {
        esquecerEmail()
      }
    } catch (e) {
      // A API devolve a mesma mensagem para e-mail inexistente e senha errada,
      // de propósito: distinguir os dois entrega a lista de quem tem conta.
      setErro(e instanceof ErroDaApi ? e.message : 'Não foi possível entrar.')
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
            <p className="mt-2 text-apoio text-texto-suave">
              Entre para acompanhar os pedidos da sua loja.
            </p>
          </div>

          <form onSubmit={aoEnviar} className="mt-6 flex flex-col gap-4">
            <Campo
              rotulo="E-mail"
              type="email"
              autoComplete="username"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />

            <CampoSenha
              rotulo="Senha"
              autoComplete="current-password"
              required
              value={senha}
              onChange={(e) => setSenha(e.target.value)}
              erro={erro ?? undefined}
            />

            <Switch
              marcado={salvarAcesso}
              onMudar={setSalvarAcesso}
              rotulo="Salvar meu acesso neste dispositivo"
            />

            <Botao type="submit" carregando={enviando} larguraTotal>
              Entrar
            </Botao>
          </form>
        </CartaoCorpo>
      </Cartao>
    </main>
  )
}
