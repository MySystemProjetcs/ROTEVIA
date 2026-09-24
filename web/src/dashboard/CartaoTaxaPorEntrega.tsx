import { useState } from 'react'
import type { FormEvent } from 'react'
import { useSessao } from '@/auth/SessaoProvider'
import { Botao } from '@/components/Botao'
import { api, ErroDaApi } from '@/lib/api'
import { centavosParaValor, extrairDigitos, formatarCentavos } from '@/lib/moeda'
import { formatarDinheiro } from '@/lib/tempo'
import { useResumoDashboard } from './useResumoDashboard'

// Cartão isolado — antes vivia dentro da FaixaResumo, junto dos KPIs de
// pedidos e receita da Central de Pedidos. Extraído para que a tela de
// Motoboys hospede a taxa (é lá que a informação faz sentido) e a Central
// mantenha só o resumo operacional. Mesmo gradiente índigo do original,
// pra não perder o destaque visual que a taxa merece na hierarquia.
const ESTILO_CARTAO_DESTAQUE: React.CSSProperties = {
  background: 'linear-gradient(135deg, rgba(79,70,229,0.22), rgba(79,70,229,0.06))',
  border: '1px solid rgba(79,70,229,0.35)',
}

export function CartaoTaxaPorEntrega({ className }: { className?: string }) {
  const { usuario } = useSessao()
  const merchantId = usuario?.merchantId ?? null
  const { resumo, erro, recarregar } = useResumoDashboard(merchantId)

  const [taxa, setTaxa] = useState<string | null>(null)
  const [salvando, setSalvando] = useState(false)
  const [mensagem, setMensagem] = useState<string | null>(null)
  const [editando, setEditando] = useState(false)

  const taxaDefinida = (resumo?.taxaPorEntrega ?? 0) > 0
  const emEdicao = editando || !taxaDefinida

  const taxaExibida =
    taxa ?? (resumo ? formatarCentavos(String(Math.round(resumo.taxaPorEntrega * 100))) : '')

  async function aoSalvarTaxa(evento: FormEvent) {
    evento.preventDefault()
    if (!merchantId) return
    const valor = taxa === null ? (resumo?.taxaPorEntrega ?? null) : centavosParaValor(taxa)
    if (valor === null || valor < 0) {
      setMensagem('Digite o valor da taxa.')
      return
    }
    setSalvando(true)
    setMensagem(null)
    try {
      await api.put(`/restaurantes/${merchantId}/taxa-entrega`, { valor })
      setTaxa(null)
      setEditando(false)
      await recarregar()
    } catch (e) {
      setMensagem(e instanceof ErroDaApi ? e.message : 'Não foi possível salvar.')
    } finally {
      setSalvando(false)
    }
  }

  // Formata "R$ 54" + ",00" separados para o estilo do Figma.
  function partesMoeda(valor: number) {
    const s = formatarDinheiro(valor)
    const [inteira, dec] = s.split(',')
    return { inteira: inteira ?? s, dec: dec ? `,${dec}` : '' }
  }

  const taxaParts = partesMoeda(resumo?.taxaPorEntrega ?? 0)

  return (
    <form onSubmit={aoSalvarTaxa} className={`rounded-[14px] p-3.5 ${className ?? ''}`} style={ESTILO_CARTAO_DESTAQUE}>
      {erro && <p className="mb-2 text-[12px] text-perigo">{erro}</p>}

      <div className="flex items-center gap-2">
        <span className="font-mono text-[11px] uppercase tracking-[0.1em] text-[#C7CBFF]">
          Taxa por entrega
        </span>
        {!emEdicao && (
          <button
            type="button"
            onClick={() => {
              setTaxa(null)
              setMensagem(null)
              setEditando(true)
            }}
            className="ml-auto rounded-[6px] bg-[rgba(255,255,255,0.08)] px-1.5 py-0.5 text-[10px] text-[#C7CBFF] hover:bg-[rgba(255,255,255,0.12)]"
          >
            EDITAR
          </button>
        )}
      </div>

      {emEdicao ? (
        <div className="mt-2 flex items-center gap-2">
          <input
            inputMode="numeric"
            placeholder="0,00"
            autoFocus={editando}
            value={taxaExibida}
            onChange={(e) => setTaxa(extrairDigitos(e.target.value))}
            className="h-9 w-full min-w-0 rounded-[8px] border border-[rgba(255,255,255,0.12)] bg-[rgba(255,255,255,0.06)] px-3 text-[13px] tabular-nums text-texto outline-none focus:border-marca-500"
          />
          <Botao type="submit" tamanho="pequeno" carregando={salvando} className="shrink-0">
            Salvar
          </Botao>
        </div>
      ) : (
        <div className="mt-1.5 flex items-baseline gap-1">
          <span className="text-[30px] font-bold tracking-tight text-texto tabular-nums">
            {taxaParts.inteira}
          </span>
          <span className="text-[18px] font-medium text-[#C7CBFF]">{taxaParts.dec}</span>
        </div>
      )}

      {!emEdicao && (
        <div className="mt-1.5 text-[11px] text-[#C7CBFF]">
          Válida para todas as zonas ativas
        </div>
      )}
      {mensagem && <p className="mt-1 text-[11px] text-perigo">{mensagem}</p>}
    </form>
  )
}
