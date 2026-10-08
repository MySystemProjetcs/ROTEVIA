import { useEffect, useRef, useState } from 'react'
import { NavLink, useLocation } from 'react-router-dom'
import { useSessao } from '@/auth/SessaoProvider'
import { AvatarUsuario } from '@/components/AvatarUsuario'
import {
  CabecalhoDaBarra,
  CascaDaBarra,
  ConteudoDaBarra,
  RodapeDaBarra,
} from '@/components/barra-lateral/Casca'
import { useBarraLateral } from '@/components/barra-lateral/contexto'
import { cn } from '@/lib/cn'
import { usePedidosAguardando } from '@/pedidos/usePedidosAguardando'
import { usePollingHealth } from '@/lib/usePollingHealth'
import { IconeMoto } from '@/components/icones/IconeMoto'
import { IconePedidos } from '@/components/icones/IconePedidos'
import { IconeMoeda } from '@/components/icones/IconeMoeda'
import { usePerfil } from '@/perfil/contexto'
import { DialogoPerfil } from '@/perfil/DialogoPerfil'

// Ícone do cardápio
function IconeCardapio({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={className}>
      <path d="M3 3h18v4H3zM3 10h18v4H3zM3 17h18v4H3z" />
    </svg>
  )
}

// Ícone de configurações
function IconeConfig({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={className}>
      <circle cx="12" cy="12" r="3" />
      <path d="M19.4 15a1.7 1.7 0 00.4 1.9l.1.1a2 2 0 11-2.8 2.8l-.1-.1a1.7 1.7 0 00-1.9-.4 1.7 1.7 0 00-1 1.5V21a2 2 0 11-4 0v-.1a1.7 1.7 0 00-1-1.5 1.7 1.7 0 00-1.9.4l-.1.1a2 2 0 11-2.8-2.8l.1-.1a1.7 1.7 0 00.4-1.9 1.7 1.7 0 00-1.5-1H3a2 2 0 110-4h.1a1.7 1.7 0 001.5-1 1.7 1.7 0 00-.4-1.9l-.1-.1a2 2 0 112.8-2.8l.1.1a1.7 1.7 0 001.9.4H9a1.7 1.7 0 001-1.5V3a2 2 0 114 0v.1a1.7 1.7 0 001 1.5 1.7 1.7 0 001.9-.4l.1-.1a2 2 0 112.8 2.8l-.1.1a1.7 1.7 0 00-.4 1.9V9a1.7 1.7 0 001.5 1H21a2 2 0 110 4h-.1a1.7 1.7 0 00-1.5 1z" />
    </svg>
  )
}

function IconeConversa({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={className}>
      <path d="M3 3v18l4-4h14V3z" />
    </svg>
  )
}

// Some por completo no trilho de ícones: sem largura pra texto, e reservar
// o espaço deixaria o ícone de baixo descentralizado. Mesma regra que
// RotuloDoGrupo já aplicava antes desta tela ganhar visual próprio.
function RotuloSecao({ children }: { children: React.ReactNode }) {
  const { expandida, ehCelular } = useBarraLateral()
  if (!ehCelular && !expandida) return null

  return (
    <span className="px-2 pt-2 pb-1 font-mono text-[10px] font-semibold uppercase tracking-[0.12em] text-texto-mudo">
      {children}
    </span>
  )
}

function ItemNav({
  para,
  icone,
  rotulo,
  badge,
  badgeText,
}: {
  para: string
  icone: React.ReactNode
  rotulo: string
  badge?: number
  badgeText?: string
}) {
  const { expandida, ehCelular } = useBarraLateral()
  const soIcone = !ehCelular && !expandida

  return (
    <NavLink
      to={para}
      end={para === '/'}
      // No trilho o rótulo vira dica do navegador — sem ela seria uma
      // fileira de ícones sem nome nenhum.
      title={soIcone ? rotulo : undefined}
      className={({ isActive }) =>
        cn(
          'flex items-center gap-2.5 rounded-[10px] py-2.5 text-[13px] transition-colors',
          soIcone ? 'justify-center px-0' : 'px-2.5',
          isActive
            ? 'border border-[rgba(79,70,229,0.35)] font-semibold text-[#EEF0FF]'
            : 'border border-transparent font-normal text-texto-suave hover:text-texto hover:bg-superficie-afundada',
        )
      }
      style={({ isActive }) =>
        isActive
          ? { background: 'linear-gradient(180deg, rgba(79,70,229,0.22), rgba(79,70,229,0.08))' }
          : {}
      }
    >
      <span className="relative shrink-0 opacity-80">
        {icone}
        {/* No trilho o número não cabe: vira uma marca no canto do ícone. */}
        {soIcone && badge !== undefined && badge > 0 && (
          <span
            aria-hidden
            className="absolute -right-1 -top-1 size-2 rounded-full bg-acento-400"
          />
        )}
      </span>

      {!soIcone && (
        <>
          <span className="flex-1 truncate">{rotulo}</span>
          {badge !== undefined && badge > 0 && (
            <span className="rounded-full bg-acento-400 px-1.5 py-0.5 font-mono text-[10px] font-bold text-texto-invertido">
              {badge}
            </span>
          )}
          {badgeText && (
            <span className="text-[11px] font-semibold text-acento-400">{badgeText}</span>
          )}
        </>
      )}
    </NavLink>
  )
}

export function BarraLateral() {
  const location = useLocation()
  const { expandida, ehCelular, definirAbertaNoCelular } = useBarraLateral()
  const { usuario, sair } = useSessao()
  const ehDono = usuario?.papel === 'DonoRestaurante'
  const ehEntregador = usuario?.papel === 'Entregador'
  const aguardando = usePedidosAguardando()
  const { online, ultimoPolling } = usePollingHealth()
  const { perfil } = usePerfil()

  const nomeDaSessao = usuario?.nomeRestaurante || usuario?.nomeUsuario || usuario?.email || ''

  const [menuAberto, setMenuAberto] = useState(false)
  const [perfilAberto, setPerfilAberto] = useState(false)
  const [configuracoesAbertas, setConfiguracoesAbertas] = useState(
    () => location.pathname.startsWith('/configuracoes/'),
  )
  const containerRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!menuAberto) return
    function aoClicarFora(e: MouseEvent) {
      if (!containerRef.current?.contains(e.target as Node)) setMenuAberto(false)
    }
    function aoTeclar(e: KeyboardEvent) {
      if (e.key === 'Escape') setMenuAberto(false)
    }
    document.addEventListener('mousedown', aoClicarFora)
    document.addEventListener('keydown', aoTeclar)
    return () => {
      document.removeEventListener('mousedown', aoClicarFora)
      document.removeEventListener('keydown', aoTeclar)
    }
  }, [menuAberto])

  return (
    <CascaDaBarra>
      {/* ── Avatar + nome da sessão (substitui o logo) ── */}
      <CabecalhoDaBarra>
        <div ref={containerRef} className="relative">
          <button
            type="button"
            aria-haspopup="menu"
            aria-expanded={menuAberto}
            onClick={() => setMenuAberto((a) => !a)}
            className="flex w-full items-center gap-2.5 rounded-[10px] px-2 py-2 text-left transition-colors hover:bg-superficie-afundada focus-visible:outline-2 focus-visible:outline-marca-600"
          >
            <AvatarUsuario
              nome={nomeDaSessao}
              foto={perfil?.fotoBase64}
              className="shrink-0 ring-1 ring-borda-forte"
            />
            <div className="min-w-0 leading-[1.15]">
              <div className="truncate text-[13px] font-semibold text-texto">{nomeDaSessao}</div>
              <div className="font-mono text-[10px] text-texto-mudo">DeliveryHub</div>
            </div>
          </button>

          {menuAberto && (
            <div
              role="menu"
              className="absolute left-0 z-10 mt-1 min-w-40 overflow-hidden rounded-[10px] border border-borda bg-superficie-alt shadow-elevado"
            >
              <button
                type="button"
                role="menuitem"
                onClick={() => { setMenuAberto(false); setPerfilAberto(true) }}
                className="w-full px-3 py-2.5 text-left text-[13px] text-texto transition-colors hover:bg-superficie-afundada focus-visible:outline-none"
              >
                Perfil
              </button>
              <button
                type="button"
                role="menuitem"
                onClick={() => { setMenuAberto(false); sair() }}
                className="w-full px-3 py-2.5 text-left text-[13px] text-texto-suave transition-colors hover:bg-superficie-afundada focus-visible:outline-none"
              >
                Sair
              </button>
            </div>
          )}
        </div>
      </CabecalhoDaBarra>

      {/* ── Navegação ───────────────────────────────────── */}
      <ConteudoDaBarra>
        <RotuloSecao>Operação</RotuloSecao>

        <ItemNav
          para="/"
          icone={<IconePedidos className="size-4" />}
          rotulo="Pedidos"
          badge={aguardando}
        />

        {ehDono && (
          <ItemNav
            para="/motoboys"
            icone={<IconeMoto className="size-4" />}
            rotulo="Motoboys"
          />
        )}

        {ehDono && (
          <ItemNav
            para="/whatsapp"
            icone={<IconeConversa />}
            rotulo="Conversas"
          />
        )}

        {ehEntregador && (
          <ItemNav
            para="/meus-ganhos"
            icone={<IconeMoeda className="size-4" />}
            rotulo="Meus ganhos"
          />
        )}

        {ehDono && (
          <>
            <RotuloSecao>Gestão</RotuloSecao>
            <ItemNav para="/cardapio"      icone={<IconeCardapio />}     rotulo="Cardápio" />
            <ItemNav para="/relatorios"    icone={<svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M3 12h18M3 6h18M3 18h18" /></svg>} rotulo="Relatórios" />
            <div>
              <button
                type="button"
                aria-expanded={configuracoesAbertas}
                aria-controls="submenu-configuracoes"
                onClick={() => setConfiguracoesAbertas((abertas) => !abertas)}
                title={!expandida ? 'Configurações' : undefined}
                className={cn(
                  'flex w-full items-center gap-2.5 rounded-[10px] py-2.5 text-[13px] transition-colors',
                  !expandida ? 'justify-center px-0' : 'px-2.5',
                  location.pathname.startsWith('/configuracoes/')
                    ? 'border border-[rgba(79,70,229,0.35)] font-semibold text-[#EEF0FF]'
                    : 'border border-transparent font-normal text-texto-suave hover:text-texto hover:bg-superficie-afundada',
                )}
                style={location.pathname.startsWith('/configuracoes/')
                  ? { background: 'linear-gradient(180deg, rgba(79,70,229,0.22), rgba(79,70,229,0.08))' }
                  : undefined}
              >
                <IconeConfig className="size-4 shrink-0" />
                {expandida && <span className="flex-1 truncate text-left">Configurações</span>}
                {expandida && (
                  <svg
                    viewBox="0 0 24 24"
                    width="14"
                    height="14"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    aria-hidden="true"
                    className={`transition-transform ${configuracoesAbertas ? 'rotate-180' : ''}`}
                  >
                    <path d="m6 9 6 6 6-6" />
                  </svg>
                )}
              </button>

              {configuracoesAbertas && (
                <div id="submenu-configuracoes" className="ml-3 mt-1 border-l border-borda pl-2">
                  <NavLink
                    to="/configuracoes/horarios"
                    onClick={() => { if (ehCelular) definirAbertaNoCelular(false) }}
                    title={!expandida ? 'Horários' : undefined}
                    className={({ isActive }) => cn(
                      'flex items-center gap-2.5 rounded-[10px] py-2 text-[12px] transition-colors',
                      !expandida ? 'justify-center px-0' : 'px-2.5',
                      isActive
                        ? 'font-semibold text-[#EEF0FF]'
                        : 'text-texto-suave hover:bg-superficie-afundada hover:text-texto',
                    )}
                  >
                    <svg viewBox="0 0 24 24" width="15" height="15" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                      <circle cx="12" cy="12" r="9" />
                      <path d="M12 7v5l3 2" />
                    </svg>
                    {expandida && <span>Horários</span>}
                  </NavLink>
                </div>
              )}
            </div>
          </>
        )}
      </ConteudoDaBarra>

      {/* ── Status iFood no rodapé ────────────────────── */}
      <RodapeDaBarra>
        <div className="mx-1 rounded-[12px] border border-borda bg-[rgba(255,255,255,0.03)] px-3 py-2.5">
          <div className="flex items-center gap-2 text-[12px] text-texto-suave">
            <span
              className="size-2 shrink-0 rounded-full bg-acento-400"
              style={{ boxShadow: '0 0 0 3px rgba(163,230,53,0.18)' }}
            />
            {online ? 'iFood conectado' : 'iFood desconectado'}
          </div>
          {ultimoPolling && (
            <div className="mt-1 font-mono text-[10px] text-texto-mudo">
              último polling · {ultimoPolling}
            </div>
          )}
        </div>
      </RodapeDaBarra>

      <DialogoPerfil aberto={perfilAberto} onFechar={() => setPerfilAberto(false)} />
    </CascaDaBarra>
  )
}
