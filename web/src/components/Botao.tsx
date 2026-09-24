import type { ButtonHTMLAttributes, CSSProperties, ReactNode } from 'react'
import { cn } from '@/lib/cn'

type Variante =
  | 'primario'
  | 'secundario'
  | 'sutil'
  | 'perigo'
  | 'acaoConfirmar'
  | 'acaoIniciarPreparo'
  | 'acaoMarcarPronto'
  | 'acaoDespachar'
type Tamanho = 'pequeno' | 'medio' | 'grande'

// Silhueta única, do "Novo pedido" do App.tsx (App.tsx:106-121):
// gradiente vertical no fundo, sombra colorida como halo, canto 10px, fonte
// semibold 13px, sem borda. As variantes só trocam a cor de fundo — a forma
// é a mesma em todo lugar; o alerta é o que muda.
//
// `perigo` e as três variantes de ação do Kanban existem porque a cor
// codifica a etapa: verde=despachar, âmbar=confirmar, roxo=preparo. Trocar
// pra tudo índigo apagaria o atalho visual da máquina de estados —
// consciente e mantido.
interface EstiloDoFundo {
  gradiente: string
  halo: string
}

const FUNDOS: Record<Variante, EstiloDoFundo | null> = {
  primario:            { gradiente: 'linear-gradient(180deg,#5A52EA,#4338CA)', halo: 'rgba(79,70,229,0.9)'  },
  perigo:              { gradiente: 'linear-gradient(180deg,#EF6A3A,#C2410C)', halo: 'rgba(194,65,12,0.8)'  },
  acaoConfirmar:       { gradiente: 'linear-gradient(180deg,#F5B341,#B45309)', halo: 'rgba(180,83,9,0.7)'   },
  acaoIniciarPreparo:  { gradiente: 'linear-gradient(180deg,#5A52EA,#4338CA)', halo: 'rgba(79,70,229,0.9)'  },
  acaoMarcarPronto:    { gradiente: 'linear-gradient(180deg,#9B6BE3,#7C3AED)', halo: 'rgba(124,58,237,0.7)' },
  acaoDespachar:       { gradiente: 'linear-gradient(180deg,#22C55E,#15803D)', halo: 'rgba(21,128,61,0.7)'  },

  // Duas variantes fogem do gradiente por natureza — CTA secundário e link:
  //   secundario: card outline, não ação principal.
  //   sutil: ação de texto (cancelar, dispensar) — nunca deve competir
  //     visualmente com o CTA principal.
  secundario: null,
  sutil: null,
}

const CLASSES_ESPECIAIS: Partial<Record<Variante, string>> = {
  secundario: 'border border-borda-forte bg-superficie text-texto hover:bg-superficie-alt',
  sutil: 'border-0 bg-transparent text-marca-600 hover:bg-marca-50',
}

// Todos com o mesmo raio e peso da fonte, o resto é altura/padding.
const TAMANHOS: Record<Tamanho, string> = {
  // Abaixo do alvo mínimo de toque de 44px (--spacing-toque) — só para o
  // botão de ação dentro do cartão recolhido do Kanban, onde a densidade da
  // grade importa mais que o toque de dedo. Não usar em tela onde o clique é
  // a única forma de avançar sem alternativa maior por perto.
  pequeno: 'h-7 px-2.5 text-[12px]',
  medio:   'h-10 px-3.5 text-[13px]',
  grande:  'h-12 px-6 text-[15px]',
}

interface BotaoProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variante?: Variante
  tamanho?: Tamanho
  carregando?: boolean
  larguraTotal?: boolean
  children: ReactNode
}

export function Botao({
  variante = 'primario',
  tamanho = 'medio',
  carregando = false,
  larguraTotal = false,
  disabled,
  className,
  style,
  children,
  ...props
}: BotaoProps) {
  const fundo = FUNDOS[variante]

  // Gradiente vive no atributo style, não em classe: o Tailwind não gera
  // linear-gradient com stops arbitrários por classe utilitária. `style`
  // vindo de quem chama tem precedência via spread — quem quiser cobrir cor
  // continua conseguindo.
  const estiloGradiente: CSSProperties | undefined = fundo
    ? {
        background: fundo.gradiente,
        boxShadow: `0 10px 24px -12px ${fundo.halo}`,
        ...style,
      }
    : style

  return (
    <button
      {...props}
      disabled={disabled || carregando}
      style={estiloGradiente}
      className={cn(
        'inline-flex items-center justify-center gap-2 rounded-[10px] font-semibold',
        'transition-colors outline-offset-2 focus-visible:outline-2 focus-visible:outline-marca-600',
        'disabled:cursor-not-allowed disabled:opacity-50',
        fundo ? 'border-0 text-white hover:brightness-110 active:brightness-95' : CLASSES_ESPECIAIS[variante],
        TAMANHOS[tamanho],
        larguraTotal && 'w-full',
        className,
      )}
    >
      {carregando && (
        <span
          aria-hidden
          className="size-4 animate-spin rounded-full border-2 border-current border-t-transparent"
        />
      )}
      {children}
    </button>
  )
}
