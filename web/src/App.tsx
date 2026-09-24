import { Route, Routes, useLocation } from 'react-router-dom'
import { useSessao } from '@/auth/SessaoProvider'
import { BarraLateral } from '@/components/BarraLateral'
import { Botao } from '@/components/Botao'
import { BarraLateralProvider } from '@/components/barra-lateral/BarraLateralProvider'
import { GatilhoDaBarra } from '@/components/barra-lateral/GatilhoDaBarra'
import { IconeBusca } from '@/components/icones/IconeBusca'
import type { DadosDoToken } from '@/lib/sessao'
import { formatarDataHora, useAgora } from '@/lib/tempo'
import { PaginaMotoboys } from '@/motoboys/PaginaMotoboys'
import { ConfirmarConvite } from '@/paginas/ConfirmarConvite'
import { Login } from '@/paginas/Login'
import { PainelOperacao } from '@/pedidos/PainelOperacao'
import { NovoPedidoProvider, useNovoPedido } from '@/pedidos/NovoPedidoContext'
import { PerfilProvider } from '@/perfil/PerfilProvider'
import { PaginaGanhos } from '@/entregador/PaginaGanhos'
import { PaginaWhatsapp } from '@/whatsapp/PaginaWhatsapp'

export function App() {
  return (
    <Routes>
      <Route path="/convite/:linkId" element={<ConfirmarConvite />} />
      <Route path="/*" element={<AreaAutenticada />} />
    </Routes>
  )
}

function AreaAutenticada() {
  const { usuario } = useSessao()
  if (!usuario) return <Login />

  return (
    <PerfilProvider>
      <BarraLateralProvider>
        {/* NovoPedidoProvider envolve tudo: o botão do header e o PainelOperacao
            compartilham o mesmo contexto. */}
        <NovoPedidoProvider>
          <PainelDaSessao usuario={usuario} />
        </NovoPedidoProvider>
      </BarraLateralProvider>
    </PerfilProvider>
  )
}

function PainelDaSessao({ usuario }: { usuario: DadosDoToken }) {
  const agora = useAgora(60_000)
  const localizacao = useLocation()
  const ehDono = usuario.papel === 'DonoRestaurante'
  const { abrir: abrirNovoPedido } = useNovoPedido()

  const titulo =
    localizacao.pathname === '/motoboys'      ? 'Motoboys'
    : localizacao.pathname === '/whatsapp'    ? 'Conversas'
    : localizacao.pathname === '/meus-ganhos' ? 'Meus ganhos'
    : 'Central de Pedidos'

  return (
    <div className="fundo-app flex min-h-dvh">
      <BarraLateral />

      <div className="flex min-w-0 flex-1 flex-col overflow-hidden">
        {/* ── Topbar ─────────────────────────────────── */}
        <header className="flex items-center gap-4 px-6 pt-5 pb-0">
          <div className="flex min-w-0 flex-col">
            <span className="font-mono text-rotulo uppercase text-texto-mudo">
              {formatarDataHora(agora)}
            </span>
            <div className="flex items-center gap-2">
              {/* Sem lg:hidden: a barra recolhe pra trilho de ícones no
                  desktop também (Casca.tsx já sabe fazer isso), não só abre
                  a gaveta no celular — o botão precisa existir nos dois
                  tamanhos de tela, senão o desktop nunca alcança o trilho. */}
              <GatilhoDaBarra />
              <h1 className="text-[22px] font-bold leading-tight tracking-tight text-texto">
                {titulo}
              </h1>
            </div>
          </div>

          <div className="flex-1" />

          {/* Busca — escondida por página em vez de "sempre visível":
              foi pedido pra sair da Central de Pedidos e de Motoboys, e não
              há tela hoje que faça uso real do que o campo dispararia (não
              há endpoint global de busca). Mantido por ora como visual, mas
              omitido onde a ausência foi pedida explicitamente. */}
          {ehDono
            && localizacao.pathname !== '/'
            && localizacao.pathname !== '/motoboys' && (
            <label
              className="hidden items-center gap-2 rounded-[10px] border border-borda bg-superficie-alt px-3 py-2 text-texto-fraco sm:flex"
              style={{ width: 280 }}
            >
              <IconeBusca className="size-3.5 shrink-0" />
              <input
                placeholder="Buscar pedido, cliente, CPF…"
                className="w-full bg-transparent text-[13px] text-texto placeholder:text-texto-mudo outline-none"
              />
              <span className="font-mono text-[10px] rounded px-1.5 py-0.5 bg-[rgba(255,255,255,0.06)] text-texto-mudo">
                ⌘K
              </span>
            </label>
          )}

          {/* Novo pedido: aciona o FormularioPedidoInterno no PainelOperacao —
              por isso só faz sentido na tela dele, "/". Migrado pro <Botao>
              padrão: a silhueta do "Novo pedido" virou o modelo do resto do
              sistema, então este mesmo botão passa a usar o componente. */}
          {ehDono && localizacao.pathname === '/' && (
            <Botao onClick={abrirNovoPedido} className="hidden sm:inline-flex">
              <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round">
                <path d="M12 5v14M5 12h14" />
              </svg>
              Novo pedido
            </Botao>
          )}
        </header>

        {/* ── Conteúdo ────────────────────────────────── */}
        <main className="flex min-h-0 flex-1 flex-col gap-4 px-6 py-4">
          {usuario.papel === 'Entregador' ? (
            <Routes>
              <Route path="/"            element={<PainelOperacao />} />
              <Route path="/meus-ganhos" element={<PaginaGanhos />} />
            </Routes>
          ) : (
            <Routes>
              <Route path="/"         element={<PainelOperacao />} />
              <Route path="/motoboys" element={<PaginaMotoboys />} />
              <Route path="/whatsapp" element={<PaginaWhatsapp />} />
            </Routes>
          )}
        </main>
      </div>
    </div>
  )
}
