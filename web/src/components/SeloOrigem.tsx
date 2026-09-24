import type { Pedido } from '@/dominio/pedido'
import { cn } from '@/lib/cn'

// Selo de onde o pedido veio — não de que estado ele está. Pedido interno não
// leva selo: ausência de selo já significa "nasceu aqui dentro", o mesmo
// raciocínio de não etiquetar o padrão.
//
// Só o iFood tem selo por ora. A 99Food está pausada (sem loja real
// cadastrada, nenhum pedido dela chega aqui) — dar cor própria a uma origem
// que não está em uso seria trabalho para um caminho inativo.
export function SeloOrigem({ origem, className }: { origem: Pedido['origem']; className?: string }) {
  if (origem !== 'IFood') return null

  return (
    <span
      className={cn(
        'inline-flex items-center rounded-controle border border-origem-ifood/25 bg-origem-ifood-fundo px-1.5 py-0.5 text-rotulo font-semibold text-origem-ifood',
        className,
      )}
    >
      iFood
    </span>
  )
}
