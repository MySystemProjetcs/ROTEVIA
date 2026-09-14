import { Route, Routes, useLocation } from 'react-router-dom'
import { useSessao } from '@/auth/SessaoProvider'
import { AvatarIniciais } from '@/components/AvatarIniciais'
import { BarraLateral } from '@/components/BarraLateral'
import { Botao } from '@/components/Botao'
import { Etiqueta } from '@/components/Etiqueta'
import { IconeRelogio } from '@/components/icones/IconeRelogio'
import type { DadosDoToken } from '@/lib/sessao'
import { formatarHora, useAgora } from '@/lib/tempo'
import { PaginaMotoboys } from '@/motoboys/PaginaMotoboys'
import { ConfirmarConvite } from '@/paginas/ConfirmarConvite'
import { Login } from '@/paginas/Login'
import { PainelOperacao } from '@/pedidos/PainelOperacao'
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

function AreaAutenticada() {
  const { usuario, sair } = useSessao()
  const agora = useAgora(1000)
  const localizacao = useLocation()

  if (!usuario) return <Login />

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

      <div className="flex flex-1 flex-col">
        <header className="border-b border-borda bg-superficie">
          <div className="flex items-center justify-between gap-4 p-4">
            <h1 className="text-titulo text-texto">{titulo}</h1>

            <div className="flex items-center gap-3">
              <span className="inline-flex items-center gap-1.5 rounded-controle border border-borda px-2.5 py-1.5 font-mono text-apoio text-texto tabular-nums">
                <IconeRelogio className="size-4 text-texto-fraco" />
                {formatarHora(agora)}
              </span>
              {usuario.papel === 'Entregador' && (
                <span className="flex items-center gap-2">
                  <AvatarIniciais
                    nome={usuario.nomeUsuario || usuario.email}
                    className="border-borda-forte bg-superficie-afundada text-texto"
                  />
                  <span className="text-apoio font-medium text-texto">
                    {usuario.nomeUsuario || usuario.email}
                  </span>
                </span>
              )}
              <Etiqueta>{ROTULO_DO_PAPEL[usuario.papel]}</Etiqueta>
              <Botao variante="secundario" onClick={sair}>
                Sair
              </Botao>
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
