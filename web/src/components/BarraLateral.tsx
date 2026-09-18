import { useEffect, useRef, useState } from 'react'
import { useSessao } from '@/auth/SessaoProvider'
import { AvatarUsuario } from '@/components/AvatarUsuario'
import {
  CabecalhoDaBarra,
  CascaDaBarra,
  ConteudoDaBarra,
  RodapeDaBarra,
  RotuloDoGrupo,
} from '@/components/barra-lateral/Casca'
import { useBarraLateral } from '@/components/barra-lateral/contexto'
import { ItemDaBarra } from '@/components/barra-lateral/ItemDaBarra'
import { IconeMoeda } from '@/components/icones/IconeMoeda'
import { IconeMoto } from '@/components/icones/IconeMoto'
import { IconePedidos } from '@/components/icones/IconePedidos'
import { IconeSair } from '@/components/icones/IconeSair'
import { IconeWhatsApp } from '@/components/icones/IconeWhatsApp'
import { cn } from '@/lib/cn'
import { usePedidosAguardando } from '@/pedidos/usePedidosAguardando'
import { usePerfil } from '@/perfil/contexto'
import { DialogoPerfil } from '@/perfil/DialogoPerfil'

// Só o dono opera Motoboys/WhatsApp — o administrador cadastra restaurantes,
// não a operação do dia a dia de uma loja específica.
export function BarraLateral() {
  const { usuario, sair } = useSessao()
  const { perfil } = usePerfil()
  const { expandida, ehCelular, definirAbertaNoCelular } = useBarraLateral()
  const ehDono = usuario?.papel === 'DonoRestaurante'
  const ehEntregador = usuario?.papel === 'Entregador'
  const aguardando = usePedidosAguardando()

  const [menuAberto, setMenuAberto] = useState(false)
  const [perfilAberto, setPerfilAberto] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)

  // Loja para o dono, pessoa para o motoboy. O e-mail é o último recurso —
  // sessão sem nenhum nome ainda precisa se identificar.
  const nomeDaSessao = usuario?.nomeRestaurante || usuario?.nomeUsuario || usuario?.email || ''
  const soIcone = !ehCelular && !expandida

  // Clique fora e Escape fecham o menu do perfil — sem isso ele fica preso
  // aberto quando a pessoa desiste e clica em outro canto da tela.
  useEffect(() => {
    if (!menuAberto) return

    function aoClicarFora(evento: MouseEvent) {
      if (!containerRef.current?.contains(evento.target as Node)) setMenuAberto(false)
    }

    function aoTeclar(evento: KeyboardEvent) {
      if (evento.key === 'Escape') setMenuAberto(false)
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
      <CabecalhoDaBarra>
        {/* Quem está logado, não a marca: o lojista e o motoboy já sabem em que
            sistema estão — o que muda de sessão para sessão é a conta. */}
        <div ref={containerRef} className="relative">
          <button
            type="button"
            aria-haspopup="menu"
            aria-expanded={menuAberto}
            onClick={() => setMenuAberto((a) => !a)}
            title={soIcone ? nomeDaSessao : undefined}
            className={cn(
              'flex w-full items-center rounded-controle p-1 text-left outline-offset-2 transition-colors hover:bg-superficie-alt focus-visible:outline-2 focus-visible:outline-marca-600',
              soIcone ? 'justify-center' : 'gap-3',
            )}
          >
            <AvatarUsuario nome={nomeDaSessao} foto={perfil?.fotoBase64} />
            {!soIcone && (
              <span className="truncate text-corpo font-semibold text-texto">{nomeDaSessao}</span>
            )}
          </button>

          {menuAberto && (
            <div
              role="menu"
              className="absolute left-0 z-10 mt-1 min-w-40 overflow-hidden rounded-controle border border-borda bg-superficie shadow-elevado"
            >
              <button
                type="button"
                role="menuitem"
                onClick={() => {
                  setMenuAberto(false)
                  setPerfilAberto(true)
                  if (ehCelular) definirAbertaNoCelular(false)
                }}
                className="w-full px-3 py-2.5 text-left text-corpo text-texto transition-colors hover:bg-superficie-alt focus-visible:bg-superficie-alt focus-visible:outline-none"
              >
                Perfil
              </button>
            </div>
          )}
        </div>
      </CabecalhoDaBarra>

      <ConteudoDaBarra>
        <RotuloDoGrupo>Operação</RotuloDoGrupo>

        <ItemDaBarra
          para="/"
          icone={<IconePedidos className="size-5 shrink-0" />}
          rotulo="Pedidos"
          contador={aguardando}
        />

        {ehEntregador && (
          <ItemDaBarra
            para="/meus-ganhos"
            icone={<IconeMoeda className="size-5 shrink-0" />}
            rotulo="Meus ganhos"
          />
        )}

        {ehDono && (
          <>
            <RotuloDoGrupo>Gestão</RotuloDoGrupo>
            <ItemDaBarra
              para="/motoboys"
              icone={<IconeMoto className="size-5 shrink-0" />}
              rotulo="Motoboys"
            />
            <ItemDaBarra
              para="/whatsapp"
              icone={<IconeWhatsApp className="size-5 shrink-0" />}
              rotulo="WhatsApp"
            />
          </>
        )}
      </ConteudoDaBarra>

      {/* Sair mora aqui, no pé da barra, e não mais no cabeçalho da página: é
          onde o modelo põe as ações da conta, e libera a faixa do topo. */}
      <RodapeDaBarra>
        <button
          type="button"
          onClick={sair}
          title={soIcone ? 'Sair' : undefined}
          className={cn(
            'flex h-toque items-center rounded-controle text-corpo font-medium text-texto-suave outline-offset-2 transition-colors hover:bg-superficie-alt hover:text-texto focus-visible:outline-2 focus-visible:outline-marca-600',
            soIcone ? 'justify-center px-0' : 'gap-3 px-3',
          )}
        >
          <IconeSair className="size-5 shrink-0" />
          {!soIcone && <span>Sair</span>}
        </button>
      </RodapeDaBarra>

      <DialogoPerfil aberto={perfilAberto} onFechar={() => setPerfilAberto(false)} />
    </CascaDaBarra>
  )
}
