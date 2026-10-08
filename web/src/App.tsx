import { Route, Routes, useLocation } from 'react-router-dom'
import { useSessao } from '@/auth/SessaoProvider'
import { BarraLateral } from '@/components/BarraLateral'
import { BarraLateralProvider } from '@/components/barra-lateral/BarraLateralProvider'
import { GatilhoDaBarra } from '@/components/barra-lateral/GatilhoDaBarra'
import { IconeBusca } from '@/components/icones/IconeBusca'
import type { DadosDoToken } from '@/lib/sessao'
import { formatarDataHora, useAgora } from '@/lib/tempo'
import { PaginaMotoboys } from '@/motoboys/PaginaMotoboys'
import { ConfirmarConvite } from '@/paginas/ConfirmarConvite'
import { Login } from '@/paginas/Login'
import { PainelOperacao } from '@/pedidos/PainelOperacao'
import { NovoPedidoProvider } from '@/pedidos/NovoPedidoContext'
import { PerfilProvider } from '@/perfil/PerfilProvider'
import { PaginaGanhos } from '@/entregador/PaginaGanhos'
import { PaginaWhatsapp } from '@/whatsapp/PaginaWhatsapp'
import { EstadoLojaIFood } from '@/pedidos/EstadoLojaIFood'
import { PaginaHorariosFuncionamento } from '@/configuracoes/PaginaHorariosFuncionamento'

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

  // Título do topbar: só nas páginas que não têm hero próprio. Na Central
  // ('/') o hero "Gestão de pedidos" vive dentro do PainelOperacao, então o
  // topbar fica sem h1 pra não repetir o rótulo.
  const titulo =
    localizacao.pathname === '/motoboys'      ? 'Motoboys'
    : localizacao.pathname === '/whatsapp'    ? 'Conversas'
    : localizacao.pathname === '/meus-ganhos' ? 'Meus ganhos'
    : localizacao.pathname.startsWith('/configuracoes/') ? 'Configurações'
    : null

  return (
    <div className="fundo-app flex h-dvh">
      <BarraLateral />

      <div className="flex min-w-0 flex-1 flex-col overflow-hidden">
        {/* ── Topbar ─────────────────────────────────── */}
        <header className="flex items-center gap-3 px-4 pt-4 pb-0 sm:gap-4 sm:px-6 sm:pt-5">
          <div className="flex min-w-0 flex-col">
            <span className="font-mono text-rotulo uppercase text-texto-mudo">
              {formatarDataHora(agora)}
            </span>
            {ehDono && localizacao.pathname === '/' && (
              <div className="mt-1.5">
                <EstadoLojaIFood
                  merchantId={usuario.merchantId}
                  nomeFallback={usuario.nomeRestaurante}
                />
              </div>
            )}
            <div className="flex items-center gap-2">
              {/* Sem lg:hidden: a barra recolhe pra trilho de ícones no
                  desktop também (Casca.tsx já sabe fazer isso), não só abre
                  a gaveta no celular — o botão precisa existir nos dois
                  tamanhos de tela, senão o desktop nunca alcança o trilho. */}
              <GatilhoDaBarra />
              {titulo && (
                <h1 className="text-[22px] font-bold leading-tight tracking-tight text-texto">
                  {titulo}
                </h1>
              )}
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

          {/* "Novo pedido" migrou para o hero do PainelOperacao — fica
              encaixado com o título "Gestão de pedidos" em vez de flutuar no
              topbar. */}
        </header>

        {/* ── Conteúdo ────────────────────────────────── */}
        {/* overflow-y-auto: o conteúdo rola aqui dentro, não no documento —
            é isso que dá a âncora para o "Mapa da operação" grudar no rodapé
            (sticky bottom-0) em vez de rolar junto com a página. */}
        <main className="flex min-h-0 flex-1 flex-col gap-4 overflow-y-auto px-4 py-4 sm:px-6">
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
              <Route path="/configuracoes/horarios" element={<PaginaHorariosFuncionamento />} />
            </Routes>
          )}
        </main>
      </div>
    </div>
  )
}
