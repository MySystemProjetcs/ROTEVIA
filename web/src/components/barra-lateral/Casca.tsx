import type { ReactNode } from 'react'
import { cn } from '@/lib/cn'
import { useBarraLateral } from './contexto'

// Larguras do modelo do shadcn: 16rem expandida, 18rem na gaveta do celular
// (dedo precisa de mais alvo), e um trilho estreito quando recolhida — largo o
// bastante para o ícone de 20px respirar dentro do alvo de toque.
const LARGURA_EXPANDIDA = 'w-64'
const LARGURA_CELULAR = 'w-72'
const LARGURA_TRILHO = 'w-[3.75rem]'

// A casca da barra: gaveta por cima do conteúdo no celular, coluna do layout no
// desktop. Recolhida no desktop ela NÃO some — vira trilho de ícones, que é o
// ponto do modelo: a navegação continua alcançável com um clique.
export function CascaDaBarra({ children }: { children: ReactNode }) {
  const { expandida, abertaNoCelular, definirAbertaNoCelular, ehCelular } = useBarraLateral()

  return (
    <>
      {/* Cortina só no celular: no desktop a barra faz parte do layout e
          escurecer a tela atrás dela não faria sentido. */}
      {ehCelular && abertaNoCelular && (
        <button
          type="button"
          aria-label="Fechar menu"
          onClick={() => definirAbertaNoCelular(false)}
          className="fixed inset-0 z-30 bg-texto/40"
        />
      )}

      <nav
        id="barra-lateral"
        aria-label="Navegação principal"
        data-estado={expandida ? 'expandida' : 'recolhida'}
        className={cn(
          'flex shrink-0 flex-col gap-2 border-r border-borda bg-superficie py-3',
          'transition-[width,transform,visibility,padding] duration-200',
          ehCelular
            ? cn(
                'fixed inset-y-0 left-0 z-40 px-3',
                LARGURA_CELULAR,
                // `invisible` junto do deslocamento tira a barra fechada da
                // ordem de foco: só transladar deixaria links navegáveis por
                // Tab fora da tela.
                abertaNoCelular ? 'visible translate-x-0' : 'invisible -translate-x-full',
              )
            : cn('static translate-x-0', expandida ? cn(LARGURA_EXPANDIDA, 'px-3') : cn(LARGURA_TRILHO, 'px-2')),
        )}
      >
        {children}
      </nav>
    </>
  )
}

export function CabecalhoDaBarra({ children }: { children: ReactNode }) {
  return <div className="flex flex-col gap-2 pb-1">{children}</div>
}

// Rola só o miolo: cabeçalho e rodapé ficam presos, como no modelo. Com muitos
// itens é o que impede o perfil de sumir junto com a lista.
export function ConteudoDaBarra({ children }: { children: ReactNode }) {
  return <div className="flex flex-1 flex-col gap-1 overflow-y-auto">{children}</div>
}

export function RodapeDaBarra({ children }: { children: ReactNode }) {
  return <div className="flex flex-col gap-1 border-t border-borda pt-2">{children}</div>
}

// Rótulo de grupo. Some quando a barra vira trilho: não há largura para texto,
// e reservá-la deixaria o ícone descentralizado.
export function RotuloDoGrupo({ children }: { children: ReactNode }) {
  const { expandida, ehCelular } = useBarraLateral()

  if (!ehCelular && !expandida) return null

  return <span className="px-3 pt-2 text-rotulo uppercase text-texto-fraco">{children}</span>
}
