import { NavLink } from 'react-router-dom'
import { useSessao } from '@/auth/SessaoProvider'
import { Logo } from '@/components/Logo'
import { IconeMoeda } from '@/components/icones/IconeMoeda'
import { IconeMoto } from '@/components/icones/IconeMoto'
import { IconePedidos } from '@/components/icones/IconePedidos'
import { IconeWhatsApp } from '@/components/icones/IconeWhatsApp'
import { cn } from '@/lib/cn'
import { usePedidosAguardando } from '@/pedidos/usePedidosAguardando'

const CLASSE_ITEM =
  'flex items-center gap-3 rounded-controle px-3 py-2.5 text-corpo font-medium transition-colors'

function ItemMenu({
  para,
  icone,
  rotulo,
  contador = 0,
}: {
  para: string
  icone: React.ReactNode
  rotulo: string
  /** Zero não desenha nada — contador vazio é ruído, não informação. */
  contador?: number
}) {
  return (
    <NavLink
      to={para}
      end={para === '/'}
      className={({ isActive }) =>
        cn(CLASSE_ITEM, isActive ? 'bg-marca-50 text-marca-600' : 'text-texto-suave hover:bg-superficie-alt')
      }
    >
      {icone}
      {rotulo}
      {contador > 0 && (
        <span
          // O número também vai no rótulo acessível: leitor de tela não
          // enxerga a bolinha.
          aria-label={`${contador} ${contador === 1 ? 'pedido aguardando' : 'pedidos aguardando'}`}
          className="ml-auto inline-flex min-w-5 items-center justify-center rounded-controle bg-perigo px-1.5 py-0.5 text-rotulo text-texto-invertido tabular-nums"
        >
          {contador > 99 ? '99+' : contador}
        </span>
      )}
    </NavLink>
  )
}

// Só o dono opera Motoboys/WhatsApp — o administrador cadastra restaurantes,
// não a operação do dia a dia de uma loja específica.
export function BarraLateral() {
  const { usuario } = useSessao()
  const ehDono = usuario?.papel === 'DonoRestaurante'
  const ehEntregador = usuario?.papel === 'Entregador'
  const aguardando = usePedidosAguardando()

  return (
    <nav className="flex w-60 shrink-0 flex-col gap-1 border-r border-borda bg-superficie p-3">
      <div className="flex flex-col gap-2 px-1 pb-3">
        <Logo tamanho="compacto" />
        <p className="text-apoio text-texto-suave">Gestão de Pedidos</p>
        {/* Só exibição: o dono opera uma loja por sessão, não há troca aqui. */}
        <p className="truncate rounded-controle border border-borda px-3 py-2 text-apoio font-medium text-texto">
          {usuario?.nomeRestaurante || usuario?.nomeUsuario || usuario?.email}
        </p>
      </div>

      <ItemMenu para="/" icone={<IconePedidos className="size-5" />} rotulo="Pedidos" contador={aguardando} />

      {ehEntregador && (
        <ItemMenu para="/meus-ganhos" icone={<IconeMoeda className="size-5" />} rotulo="Meus ganhos" />
      )}

      {ehDono && (
        <>
          <ItemMenu para="/motoboys" icone={<IconeMoto className="size-5" />} rotulo="Motoboys" />
          <ItemMenu para="/whatsapp" icone={<IconeWhatsApp className="size-5" />} rotulo="WhatsApp" />
        </>
      )}
    </nav>
  )
}
