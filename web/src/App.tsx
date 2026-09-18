import { Route, Routes, useLocation } from 'react-router-dom'
import { useSessao } from '@/auth/SessaoProvider'
import { AvatarUsuario } from '@/components/AvatarUsuario'
import { BarraLateral } from '@/components/BarraLateral'
import { BarraLateralProvider } from '@/components/barra-lateral/BarraLateralProvider'
import { GatilhoDaBarra } from '@/components/barra-lateral/GatilhoDaBarra'
import { Etiqueta } from '@/components/Etiqueta'
import { IconeRelogio } from '@/components/icones/IconeRelogio'
import type { DadosDoToken } from '@/lib/sessao'
import { formatarHora, useAgora } from '@/lib/tempo'
import { PaginaMotoboys } from '@/motoboys/PaginaMotoboys'
import { ConfirmarConvite } from '@/paginas/ConfirmarConvite'
import { Login } from '@/paginas/Login'
import { PainelOperacao } from '@/pedidos/PainelOperacao'
import { usePerfil } from '@/perfil/contexto'
import { PerfilProvider } from '@/perfil/PerfilProvider'
import { PaginaGanhos } from '@/entregador/PaginaGanhos'
import { PaginaWhatsapp } from '@/whatsapp/PaginaWhatsapp'

const ROTULO_DO_PAPEL: Record<DadosDoToken['papel'], string> = {
  AdministradorSistema: 'Administrador',
  DonoRestaurante: 'Loja',
  Entregador: 'Entregador',
}

export function App() {
  return (
    <Routes>
      {/* Sem sessão: o motoboy chega aqui direto do link do WhatsApp. */}
      <Route path="/convite/:linkId" element={<ConfirmarConvite />} />
      <Route path="/*" element={<AreaAutenticada />} />
    </Routes>
  )
}

// A sessão é quem decide se há tela; o perfil só existe depois disso. Separar
// os dois evita buscar /perfil sem token e quebrar a tela de login.
function AreaAutenticada() {
  const { usuario } = useSessao()

  if (!usuario) return <Login />

  return (
    <PerfilProvider>
      <BarraLateralProvider>
        <PainelDaSessao usuario={usuario} />
      </BarraLateralProvider>
    </PerfilProvider>
  )
}

function PainelDaSessao({ usuario }: { usuario: DadosDoToken }) {
  const { perfil } = usePerfil()
  const agora = useAgora(1000)
  const localizacao = useLocation()
  const foto = perfil?.fotoBase64

  const titulo =
    localizacao.pathname === '/motoboys'
      ? 'Motoboys'
      : localizacao.pathname === '/whatsapp'
        ? 'WhatsApp'
        : localizacao.pathname === '/meus-ganhos'
          ? 'Meus ganhos'
          : 'Painel de Pedidos'

  return (
    <div className="flex min-h-dvh bg-superficie-alt">
      <BarraLateral />

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="border-b border-borda bg-superficie">
          <div className="flex items-center justify-between gap-3 p-4">
            <div className="flex min-w-0 items-center gap-2">
              <GatilhoDaBarra />
              <h1 className="truncate text-titulo text-texto">{titulo}</h1>
            </div>

            <div className="flex items-center gap-3">
              <span className="hidden items-center gap-1.5 rounded-controle border border-borda px-2.5 py-1.5 font-mono text-apoio text-texto tabular-nums sm:inline-flex">
                <IconeRelogio className="size-4 text-texto-fraco" />
                {formatarHora(agora)}
              </span>
              {usuario.papel === 'Entregador' && (
                <span className="flex items-center gap-2">
                  <AvatarUsuario
                    nome={usuario.nomeUsuario || usuario.email}
                    foto={foto}
                    className="border-borda-forte bg-superficie-afundada text-texto"
                  />
                  {/* O nome some no celular: o avatar já identifica, e a linha
                      do cabeçalho não comporta nome inteiro mais etiqueta mais
                      botão numa tela de 375px. */}
                  <span className="hidden text-apoio font-medium text-texto sm:inline">
                    {usuario.nomeUsuario || usuario.email}
                  </span>
                </span>
              )}
              <Etiqueta>{ROTULO_DO_PAPEL[usuario.papel]}</Etiqueta>
            </div>
          </div>
        </header>

        <main className="flex-1 p-4">
          {usuario.papel === 'Entregador' ? (
            // Mesmo sistema do dono — o Kanban decide sozinho, pelo papel da
            // sessão, quais colunas e ações mostrar.
            <Routes>
              <Route path="/" element={<PainelOperacao />} />
              <Route path="/meus-ganhos" element={<PaginaGanhos />} />
            </Routes>
          ) : (
            <Routes>
              <Route path="/" element={<PainelOperacao />} />
              <Route path="/motoboys" element={<PaginaMotoboys />} />
              <Route path="/whatsapp" element={<PaginaWhatsapp />} />
            </Routes>
          )}
        </main>
      </div>
    </div>
  )
}
