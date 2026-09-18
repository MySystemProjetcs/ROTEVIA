import type { ReactNode } from 'react'
import { NavLink } from 'react-router-dom'
import { cn } from '@/lib/cn'
import { useBarraLateral } from './contexto'

interface ItemDaBarraProps {
  para: string
  icone: ReactNode
  rotulo: string
  /** Zero não desenha nada — contador vazio é ruído, não informação. */
  contador?: number
}

export function ItemDaBarra({ para, icone, rotulo, contador = 0 }: ItemDaBarraProps) {
  const { expandida, ehCelular, definirAbertaNoCelular } = useBarraLateral()
  const soIcone = !ehCelular && !expandida

  return (
    <NavLink
      to={para}
      end={para === '/'}
      // No celular a gaveta cobre a tela: deixá-la aberta sobre a página que
      // acabou de abrir esconderia justamente o que a pessoa foi ver. No
      // desktop a barra é a navegação da página e fica como está.
      onClick={() => {
        if (ehCelular) definirAbertaNoCelular(false)
      }}
      // Recolhida, o rótulo vira dica do navegador — sem ela o trilho seria uma
      // fileira de ícones sem nome.
      title={soIcone ? rotulo : undefined}
      className={({ isActive }) =>
        cn(
          'relative flex h-toque items-center rounded-controle text-corpo font-medium outline-offset-2 transition-colors focus-visible:outline-2 focus-visible:outline-marca-600',
          soIcone ? 'justify-center px-0' : 'gap-3 px-3',
          isActive ? 'bg-marca-50 text-marca-600' : 'text-texto-suave hover:bg-superficie-alt',
        )
      }
    >
      {icone}

      {!soIcone && <span className="truncate">{rotulo}</span>}

      {contador > 0 &&
        (soIcone ? (
          // No trilho o número não cabe: vira marca no canto do ícone, e a
          // contagem exata continua no rótulo acessível.
          <span
            aria-label={`${contador} ${contador === 1 ? 'pedido aguardando' : 'pedidos aguardando'}`}
            className="absolute right-1.5 top-1.5 size-2 rounded-full bg-perigo"
          />
        ) : (
          <span
            // O número também vai no rótulo acessível: leitor de tela não
            // enxerga a bolinha.
            aria-label={`${contador} ${contador === 1 ? 'pedido aguardando' : 'pedidos aguardando'}`}
            className="ml-auto inline-flex min-w-5 items-center justify-center rounded-controle bg-perigo px-1.5 py-0.5 text-rotulo text-texto-invertido tabular-nums"
          >
            {contador > 99 ? '99+' : contador}
          </span>
        ))}
    </NavLink>
  )
}
