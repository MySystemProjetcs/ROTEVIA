import { cn } from '@/lib/cn'

interface AlternadorDisponibilidadeProps {
  disponivel: boolean | null
  erro: string | null
  definir: (disponivel: boolean) => Promise<void> | void
}

// O único controle manual de presença: Online/Offline. "Em Entrega" não é
// opção — aparece sozinho quando há entrega ativa alocada.
//
// O estado vem por prop, não de hook próprio: o painel também precisa saber se
// o motoboy está online (é o que libera o envio de GPS), e duas chamadas ao
// useDisponibilidade seriam duas buscas e dois estados que divergem ao clicar.
export function AlternadorDisponibilidade({
  disponivel,
  erro,
  definir,
}: AlternadorDisponibilidadeProps) {
  return (
    <div className="flex items-center gap-3">
      {erro && <span className="text-apoio text-perigo">{erro}</span>}

      <div
        role="group"
        aria-label="Disponibilidade para entregas"
        className="inline-flex rounded-controle border border-borda bg-superficie p-1"
      >
        <button
          type="button"
          disabled={disponivel === null}
          onClick={() => void definir(true)}
          aria-pressed={disponivel === true}
          className={cn(
            'rounded-[0.375rem] px-3 py-1.5 text-apoio font-semibold transition-colors disabled:opacity-50',
            disponivel === true ? 'bg-sucesso text-texto-invertido' : 'text-texto-suave hover:bg-superficie-alt',
          )}
        >
          Online
        </button>
        <button
          type="button"
          disabled={disponivel === null}
          onClick={() => void definir(false)}
          aria-pressed={disponivel === false}
          className={cn(
            'rounded-[0.375rem] px-3 py-1.5 text-apoio font-semibold transition-colors disabled:opacity-50',
            disponivel === false
              ? 'bg-superficie-afundada text-texto'
              : 'text-texto-suave hover:bg-superficie-alt',
          )}
        >
          Offline
        </button>
      </div>
    </div>
  )
}
