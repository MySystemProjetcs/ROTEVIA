import { useState } from 'react'
import type { FormEvent } from 'react'
import imagemCozinha from '@/assets/d1456210-b6e1-4a62-8dcd-49ea3bcc6abe.jpeg'
import iconeApp from '@/assets/toolsdelivery-icon.png'
import { esquecerEmail, lerEmailSalvo, pedirParaNavegadorSalvar, salvarEmail } from '@/auth/acessoSalvo'
import { useSessao } from '@/auth/SessaoProvider'
import { Botao } from '@/components/Botao'
import { Campo } from '@/components/Campo'
import { CampoSenha } from '@/components/CampoSenha'
import { Checkbox } from '@/components/Checkbox'
import { ErroDaApi } from '@/lib/api'

// Sequência de entrada: marca → contexto → boas-vindas → campos → ação. Os
// atrasos são milissegundos a partir do mount; o keyframe `entrada` vive no
// bloco <style> abaixo e é anulado quando o viewer prefere menos movimento.
const SEQUENCIA = {
  marcaEsquerda: 0,
  rotulo: 80,
  titulo: 160,
  subtexto: 240,
  imagem: 320,
  chips: 400,
  topbar: 0,
  rotuloBemVindo: 160,
  tituloBemVindo: 240,
  subtextoBemVindo: 320,
  formulario: 400,
  dica: 560,
} as const

function estiloEntrada(delayMs: number): React.CSSProperties {
  return {
    animation: 'toolsdelivery-entrada 480ms ease-out both',
    animationDelay: `${delayMs}ms`,
  }
}

export function Login() {
  const { entrar } = useSessao()
  const [email, setEmail] = useState(lerEmailSalvo)
  const [senha, setSenha] = useState('')
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
      setErro(e instanceof ErroDaApi ? e.message : 'Não foi possível entrar.')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <main className="grid min-h-dvh grid-cols-1 bg-fundo lg:grid-cols-[1.1fr_1fr]">
      {/* Keyframes locais: fade + 12px, 480ms ease-out, respeita
          prefers-reduced-motion (vira identidade imediata). Vivem na página
          pra não poluir o theme.css global com animação só desta tela. */}
      <style>{`
        @keyframes toolsdelivery-entrada {
          from { opacity: 0; transform: translateY(12px); }
          to   { opacity: 1; transform: translateY(0); }
        }
        @media (prefers-reduced-motion: reduce) {
          [data-entrada] { animation: none !important; opacity: 1 !important; transform: none !important; }
        }
      `}</style>

      {/* ── Painel esquerdo: marca + narrativa + fluxo ────────────────────── */}
      <section className="relative flex flex-col justify-between gap-10 overflow-hidden bg-superficie px-8 py-10 lg:px-12 lg:py-14">
        <div data-entrada style={estiloEntrada(SEQUENCIA.marcaEsquerda)} className="flex items-center gap-3">
          <LogoToolsDelivery />
          <span className="text-titulo text-texto">ToolsDelivery</span>
        </div>

        <div className="flex max-w-xl flex-col gap-5">
          <span
            data-entrada
            style={estiloEntrada(SEQUENCIA.rotulo)}
            className="text-rotulo uppercase tracking-[0.14em] text-marca-400"
          >
            Gestão de pedidos para restaurantes
          </span>
          <h1
            data-entrada
            style={estiloEntrada(SEQUENCIA.titulo)}
            className="text-[48px] font-bold leading-[1.08] tracking-tight text-texto lg:text-[56px]"
          >
            Cada pedido, um caminho{' '}
            <span className="text-marca-400">bem cuidado.</span>
          </h1>
          <p
            data-entrada
            style={estiloEntrada(SEQUENCIA.subtexto)}
            className="text-corpo text-texto-suave"
          >
            Do primeiro pedido à última entrega, conecte sua equipe e acompanhe
            cada etapa em um só lugar.
          </p>
        </div>

        <div className="flex flex-col gap-5">
          {/* Imagem ambiente da cozinha. Esconde em telas estreitas pra
              priorizar o formulário no mobile — o painel esquerdo inteiro
              some em telas < lg (ver grid do <main>). */}
          <div
            data-entrada
            style={estiloEntrada(SEQUENCIA.imagem)}
            className="hidden h-48 w-full overflow-hidden rounded-cartao border border-borda sm:block lg:h-72"
          >
            <img
              src={imagemCozinha}
              alt="Equipe de cozinha preparando um prato"
              loading="lazy"
              className="size-full object-cover"
            />
          </div>

          <div
            data-entrada
            style={estiloEntrada(SEQUENCIA.chips)}
            className="flex flex-wrap items-center gap-2"
          >
            <ChipFluxo titulo="Pedido" subtitulo="Tudo começa aqui" icone={<IconeNota />} />
            <SetaFluxo />
            <ChipFluxo titulo="Cozinha" subtitulo="Preparo organizado" icone={<IconeChef />} />
            <SetaFluxo />
            <ChipFluxo titulo="Entrega" subtitulo="Até o cliente" icone={<IconeMotoLogin />} />
          </div>
        </div>

        <p className="flex items-center gap-3 text-apoio text-texto-suave">
          <span aria-hidden className="h-px w-6 bg-marca-500" />
          Mais clareza para a equipe. Mais cuidado em cada entrega.
        </p>
      </section>

      {/* ── Painel direito: formulário ────────────────────────────────────── */}
      <section className="relative flex flex-col bg-superficie-alt px-6 py-6 lg:px-12 lg:py-8">
        <header
          data-entrada
          style={estiloEntrada(SEQUENCIA.topbar)}
          className="flex items-center justify-between text-apoio text-texto-fraco"
        >
          <span>Central de pedidos</span>
          <span className="inline-flex items-center gap-1.5">
            <IconeCadeado /> Acesso da equipe
          </span>
        </header>

        <div className="flex flex-1 items-center">
          <form
            onSubmit={aoEnviar}
            noValidate
            className="mx-auto flex w-full max-w-md flex-col gap-5"
            aria-labelledby="login-titulo"
          >
            <span
              data-entrada
              style={estiloEntrada(SEQUENCIA.rotuloBemVindo)}
              className="text-rotulo uppercase tracking-[0.14em] text-marca-400"
            >
              Bem-vindo à ToolsDelivery
            </span>
            <h2
              id="login-titulo"
              data-entrada
              style={estiloEntrada(SEQUENCIA.tituloBemVindo)}
              className="text-[32px] font-bold leading-tight tracking-tight text-texto"
            >
              Sua operação começa aqui.
            </h2>
            <p
              data-entrada
              style={estiloEntrada(SEQUENCIA.subtextoBemVindo)}
              className="text-apoio text-texto-suave"
            >
              Entre com seu e-mail e senha para acessar a gestão do seu
              restaurante.
            </p>

            <div data-entrada style={estiloEntrada(SEQUENCIA.formulario)} className="flex flex-col gap-4">
              <Campo
                rotulo="E-mail"
                type="email"
                autoComplete="username"
                required
                placeholder="voce@restaurante.com.br"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />

              <CampoSenha
                rotulo="Senha"
                autoComplete="current-password"
                required
                placeholder="Digite sua senha"
                value={senha}
                onChange={(e) => setSenha(e.target.value)}
                erro={erro ?? undefined}
              />

              <Checkbox
                marcado={salvarAcesso}
                onMudar={setSalvarAcesso}
                rotulo="Salvar acesso neste dispositivo"
              />

              <Botao type="submit" carregando={enviando} larguraTotal>
                Entrar
              </Botao>

              <a
                href="#recuperar-senha"
                className="self-center text-apoio font-medium text-marca-400 underline underline-offset-4 hover:text-marca-300"
              >
                Esqueceu sua senha?
              </a>
            </div>

            <div
              data-entrada
              style={estiloEntrada(SEQUENCIA.dica)}
              className="mt-2 flex items-start gap-2 border-t border-borda pt-5 text-apoio text-texto-fraco"
              role="note"
            >
              <IconeMonitor />
              <span>
                Em um dispositivo compartilhado, mantenha a opção de salvar
                acesso desmarcada.
              </span>
            </div>
          </form>
        </div>

        <footer className="pt-4 text-center text-apoio text-texto-fraco">
          ToolsDelivery · Gestão que acompanha seu restaurante.
        </footer>
      </section>
    </main>
  )
}

// ── Marcas gráficas e ícones da tela de login ──────────────────────────────
function LogoToolsDelivery() {
  return (
    <img
      src={iconeApp}
      alt=""
      aria-hidden
      className="size-10 rounded-cartao"
    />
  )
}

function ChipFluxo({
  titulo,
  subtitulo,
  icone,
}: {
  titulo: string
  subtitulo: string
  icone: React.ReactNode
}) {
  return (
    <span className="inline-flex items-center gap-2.5 rounded-cartao border border-borda bg-superficie-alt px-3 py-2.5">
      <span className="flex size-8 items-center justify-center rounded-controle bg-superficie-afundada text-texto-suave">
        {icone}
      </span>
      <span className="flex flex-col leading-tight">
        <span className="text-apoio font-semibold text-texto">{titulo}</span>
        <span className="text-rotulo normal-case tracking-normal text-texto-fraco">
          {subtitulo}
        </span>
      </span>
    </span>
  )
}

function SetaFluxo() {
  return (
    <span aria-hidden className="text-texto-fraco">
      <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M5 12h14M13 6l6 6-6 6" />
      </svg>
    </span>
  )
}

function IconeNota() {
  return (
    <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="5" y="3" width="14" height="18" rx="2" />
      <path d="M9 7h6M9 11h6M9 15h4" />
    </svg>
  )
}

function IconeChef() {
  return (
    <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M6 11a3 3 0 013-3 3 3 0 013-2 3 3 0 013 2 3 3 0 013 3v2H6z" />
      <path d="M7 15h10v3a2 2 0 01-2 2H9a2 2 0 01-2-2z" />
    </svg>
  )
}

function IconeMotoLogin() {
  return (
    <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="6" cy="17" r="2.5" />
      <circle cx="18" cy="17" r="2.5" />
      <path d="M8.5 17h7l-2-6h-5zM13.5 11l1-3h3" />
    </svg>
  )
}

function IconeCadeado() {
  return (
    <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="5" y="11" width="14" height="9" rx="2" />
      <path d="M8 11V8a4 4 0 018 0v3" />
    </svg>
  )
}

function IconeMonitor() {
  return (
    <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="mt-0.5 shrink-0">
      <rect x="3" y="5" width="18" height="12" rx="2" />
      <path d="M9 21h6M12 17v4" />
    </svg>
  )
}
