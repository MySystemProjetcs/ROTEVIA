import { useState } from 'react'
import type { FormEvent } from 'react'
import { Botao } from '@/components/Botao'
import { IconeLapis } from '@/components/icones/IconeLapis'
import { useSessao } from '@/auth/SessaoProvider'
import { api, ErroDaApi } from '@/lib/api'
import { centavosParaValor, extrairDigitos, formatarCentavos } from '@/lib/moeda'
import { formatarDinheiro } from '@/lib/tempo'
import { useResumoDashboard } from './useResumoDashboard'

export function FaixaResumo() {
  const { usuario } = useSessao()
  const merchantId = usuario?.merchantId ?? null
  const { resumo, erro, recarregar } = useResumoDashboard(merchantId)

  // Dígitos dos centavos (nulo = "mostra o vigente do servidor"). O campo
  // exibe a máscara derivada — o que o dono digita nunca é texto com vírgula.
  const [taxa, setTaxa] = useState<string | null>(null)
  const [salvando, setSalvando] = useState(false)
  const [mensagem, setMensagem] = useState<string | null>(null)
  const [editando, setEditando] = useState(false)

  // Taxa é coisa que se define uma vez e quase nunca se mexe. Depois de salva
  // ela vira leitura, e o campo só reaparece pelo lápis — assim o valor não
  // fica exposto a alteração acidental no meio da operação.
  // Zerada (nunca definida) já abre em edição: não há valor para mostrar.
  const taxaDefinida = (resumo?.taxaPorEntrega ?? 0) > 0
  const emEdicao = editando || !taxaDefinida

  // Derivado no render, sem efeito — evita render em cascata.
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
      setMensagem(null)
      setEditando(false)
      await recarregar()
    } catch (e) {
      setMensagem(e instanceof ErroDaApi ? e.message : 'Não foi possível salvar a taxa.')
    } finally {
      setSalvando(false)
    }
  }

  return (
    <div className="flex flex-col gap-3">
      {erro && <p className="text-apoio text-perigo">{erro}</p>}

      <div className="grid grid-cols-2 gap-3 xl:grid-cols-4">
        <div className="rounded-cartao border border-borda bg-superficie p-4 shadow-cartao">
          <p className="text-rotulo uppercase text-texto-suave">Motoboys online</p>
          <p className="text-destaque tabular-nums text-texto">
            {resumo?.motoboysOnline ?? '—'}
            <span className="text-apoio font-normal text-texto-fraco">
              {' '}
              · {resumo?.motoboysEmEntrega ?? '—'} em entrega
            </span>
          </p>
        </div>

        <div className="rounded-cartao border border-borda bg-superficie p-4 shadow-cartao">
          <p className="text-rotulo uppercase text-texto-suave">Pedidos hoje</p>
          <p className="text-destaque tabular-nums text-texto">{resumo?.qtdPedidosHoje ?? '—'}</p>
        </div>

        <div className="rounded-cartao border border-borda bg-superficie p-4 shadow-cartao">
          <p className="text-rotulo uppercase text-texto-suave">Receita hoje</p>
          <p className="text-destaque tabular-nums text-texto">
            {resumo ? formatarDinheiro(resumo.receitaHoje) : '—'}
          </p>
          {/* O total inclui pedido de teste — sem este aviso o lojista leria
              faturamento de sandbox como venda de verdade. */}
          {resumo && resumo.qtdDeTeste > 0 && (
            <p className="text-apoio text-alerta">
              inclui {resumo.qtdDeTeste} {resumo.qtdDeTeste === 1 ? 'pedido de teste' : 'pedidos de teste'}
            </p>
          )}
        </div>

        <form
          onSubmit={aoSalvarTaxa}
          className="rounded-cartao border border-borda bg-superficie p-4 shadow-cartao"
        >
          <label
            htmlFor={emEdicao ? 'taxa-entrega' : undefined}
            className="text-rotulo uppercase text-texto-suave"
          >
            Taxa por entrega (R$)
          </label>

          {emEdicao ? (
            <div className="mt-1 flex items-center gap-2">
              <input
                id="taxa-entrega"
                inputMode="numeric"
                placeholder="0,00"
                autoFocus={editando}
                value={taxaExibida}
                onChange={(e) => setTaxa(extrairDigitos(e.target.value))}
                className="h-toque w-full min-w-0 rounded-controle border border-borda-forte bg-superficie px-3 text-corpo text-texto tabular-nums outline-offset-2 focus-visible:outline-2 focus-visible:outline-marca-600"
              />
              <Botao type="submit" carregando={salvando}>
                Salvar
              </Botao>
            </div>
          ) : (
            <div className="mt-1 flex items-center justify-between gap-2">
              <p className="text-destaque tabular-nums text-texto">
                {formatarDinheiro(resumo?.taxaPorEntrega ?? 0)}
              </p>
              <button
                type="button"
                onClick={() => {
                  setTaxa(null)
                  setMensagem(null)
                  setEditando(true)
                }}
                aria-label="Alterar taxa por entrega"
                className="inline-flex size-toque items-center justify-center rounded-controle text-texto-suave transition-colors outline-offset-2 hover:bg-superficie-alt hover:text-texto focus-visible:outline-2 focus-visible:outline-marca-600"
              >
                <IconeLapis className="size-5" />
              </button>
            </div>
          )}
        </form>
      </div>

      {mensagem && <p className="text-apoio text-perigo">{mensagem}</p>}
    </div>
  )
}
