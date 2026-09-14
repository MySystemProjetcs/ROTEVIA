import { useState } from 'react'
import { useCallback, useEffect } from 'react'
import { Botao } from '@/components/Botao'
import { IconeLocal } from '@/components/icones/IconeLocal'
import { api, ErroDaApi } from '@/lib/api'
import { formatarDinheiro } from '@/lib/tempo'

// Data no formato que o <input type="date"> usa (yyyy-MM-dd), montada a partir
// do relógio local — toISOString() daria o dia em UTC e viraria o dia anterior
// depois das 21h no horário de Brasília.
function paraCampoDeData(data: Date): string {
  const mes = String(data.getMonth() + 1).padStart(2, '0')
  const dia = String(data.getDate()).padStart(2, '0')

  return `${data.getFullYear()}-${mes}-${dia}`
}

interface ItemGanho {
  pedidoId: string
  numeroExibicao: string
  nomeLoja: string
  valor: number
  recebidoEm: string
  clienteNome: string
  enderecoResumido: string | null
  itens: string[]
}

interface ResultadoGanhos {
  total: number
  qtdEntregas: number
  itens: ItemGanho[]
  pagina: number
  tamanhoPagina: number
}

// Relatório não precisa da resolução do quadro operacional: 15s basta.
const INTERVALO_MS = 15_000

const CLASSE_CAMPO_DATA =
  'h-toque rounded-controle border border-borda-forte bg-superficie px-3 text-corpo text-texto outline-offset-2 focus-visible:outline-2 focus-visible:outline-marca-600'


function formatarDataCurta(iso: string): string {
  return new Date(iso).toLocaleString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}

export function PaginaGanhos() {
  const hoje = paraCampoDeData(new Date())
  const [inicio, setInicio] = useState(hoje)
  const [fim, setFim] = useState(hoje)
  const [pagina, setPagina] = useState(1)
  const [ganhos, setGanhos] = useState<ResultadoGanhos | null>(null)
  const [erro, setErro] = useState<string | null>(null)

  const intervaloInvertido = fim < inicio

  const recarregar = useCallback(async () => {
    // Não vale gastar requisição com intervalo que o servidor vai recusar.
    if (fim < inicio) return

    try {
      setGanhos(
        await api.get<ResultadoGanhos>(
          `/entregador/ganhos?inicio=${inicio}&fim=${fim}&pagina=${pagina}`,
        ),
      )
      setErro(null)
    } catch (e) {
      setErro(e instanceof ErroDaApi ? e.message : 'Não foi possível carregar os ganhos.')
    }
  }, [inicio, fim, pagina])

  // Mudar o intervalo reinicia a paginação: continuar na página 4 depois de
  // encurtar o período cairia numa lista que talvez nem tenha 4 páginas.
  function trocarInicio(valor: string) {
    setInicio(valor)
    setPagina(1)
  }

  function trocarFim(valor: string) {
    setFim(valor)
    setPagina(1)
  }

  const totalPaginas = ganhos ? Math.max(1, Math.ceil(ganhos.qtdEntregas / ganhos.tamanhoPagina)) : 1

  // O ganho é gravado quando a entrega termina — no clique do próprio motoboy
  // em Finalizar, ou no CONCLUDED que o iFood manda. Nenhum dos dois passa por
  // esta tela, então ela precisa buscar de novo sozinha.
  //
  // Poll em vez de SignalR: o entregador não mantém conexão com o Hub (só o
  // painel da loja mantém), e abrir uma conexão dedicada para uma tela de
  // relatório custaria mais do que vale.
  useEffect(() => {
    void recarregar()

    const id = setInterval(() => void recarregar(), INTERVALO_MS)
    // Voltar para a aba é o momento mais provável de o valor ter mudado, e
    // aba em segundo plano tem o temporizador estrangulado pelo navegador.
    const aoFocar = () => void recarregar()
    window.addEventListener('focus', aoFocar)

    return () => {
      clearInterval(id)
      window.removeEventListener('focus', aoFocar)
    }
  }, [recarregar])

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex flex-col gap-1">
          <label htmlFor="ganhos-inicio" className="text-rotulo uppercase text-texto-suave">
            De
          </label>
          <input
            id="ganhos-inicio"
            type="date"
            value={inicio}
            max={fim}
            onChange={(e) => trocarInicio(e.target.value)}
            className={CLASSE_CAMPO_DATA}
          />
        </div>

        <div className="flex flex-col gap-1">
          <label htmlFor="ganhos-fim" className="text-rotulo uppercase text-texto-suave">
            Até
          </label>
          <input
            id="ganhos-fim"
            type="date"
            value={fim}
            min={inicio}
            onChange={(e) => trocarFim(e.target.value)}
            className={CLASSE_CAMPO_DATA}
          />
        </div>

        <Botao variante="secundario" onClick={() => { trocarInicio(hoje); trocarFim(hoje) }}>
          Hoje
        </Botao>
      </div>

      {intervaloInvertido && (
        <p className="text-apoio text-perigo">A data final não pode ser anterior à inicial.</p>
      )}

      {erro && <p className="text-apoio text-perigo">{erro}</p>}

      <div className="grid grid-cols-2 gap-3 xl:grid-cols-4">
        <div className="rounded-cartao border border-borda bg-superficie p-4 shadow-cartao">
          <p className="text-rotulo uppercase text-texto-suave">Total no período</p>
          <p className="text-destaque tabular-nums text-texto">
            {ganhos ? formatarDinheiro(ganhos.total) : '—'}
          </p>
        </div>
        <div className="rounded-cartao border border-borda bg-superficie p-4 shadow-cartao">
          <p className="text-rotulo uppercase text-texto-suave">Entregas</p>
          <p className="text-destaque tabular-nums text-texto">{ganhos?.qtdEntregas ?? '—'}</p>
        </div>
      </div>

      <div className="overflow-x-auto rounded-cartao border border-borda bg-superficie shadow-cartao">
        <table className="w-full min-w-[560px] border-collapse text-left">
          <thead>
            <tr className="border-b border-borda">
              <th scope="col" className="px-4 py-3 text-rotulo uppercase text-texto-fraco">Pedido</th>
              <th scope="col" className="px-4 py-3 text-rotulo uppercase text-texto-fraco">Loja</th>
              <th scope="col" className="px-4 py-3 text-rotulo uppercase text-texto-fraco">Recebido em</th>
              <th scope="col" className="px-4 py-3 text-right text-rotulo uppercase text-texto-fraco">Valor</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-borda">
            {!ganhos ? (
              <tr>
                <td colSpan={4} className="px-4 py-6 text-apoio text-texto-suave">Carregando...</td>
              </tr>
            ) : ganhos.itens.length === 0 ? (
              <tr>
                <td colSpan={4} className="px-4 py-6 text-apoio text-texto-suave">
                  Nenhuma entrega concluída neste período.
                </td>
              </tr>
            ) : (
              ganhos.itens.map((item) => (
                <tr key={item.pedidoId}>
                  {/* A coluna do pedido carrega o contexto da corrida: só o
                      número não deixa o motoboy reconhecer qual entrega foi. */}
                  <td className="px-4 py-3 align-top">
                    <p className="text-corpo font-semibold text-texto">#{item.numeroExibicao}</p>
                    <p className="text-apoio text-texto-suave">{item.clienteNome}</p>
                    {item.enderecoResumido && (
                      <p className="flex items-start gap-1.5 text-apoio text-texto-suave">
                        <IconeLocal className="mt-0.5 size-4 shrink-0 text-texto-fraco" />
                        <span>{item.enderecoResumido}</span>
                      </p>
                    )}
                    {item.itens.length > 0 && (
                      <ul className="mt-1 flex flex-col gap-0.5">
                        {item.itens.map((descricao) => (
                          <li key={descricao} className="text-apoio text-texto-fraco">
                            {descricao}
                          </li>
                        ))}
                      </ul>
                    )}
                  </td>
                  <td className="px-4 py-3 align-top text-corpo text-texto">{item.nomeLoja}</td>
                  <td className="px-4 py-3 align-top text-apoio text-texto-suave tabular-nums">
                    {formatarDataCurta(item.recebidoEm)}
                  </td>
                  <td className="px-4 py-3 text-right align-top text-corpo font-semibold text-sucesso tabular-nums">
                    {formatarDinheiro(item.valor)}
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {/* Só aparece quando há mais de uma página — controle de navegação sem
          para onde navegar é ruído. */}
      {ganhos && totalPaginas > 1 && (
        <nav aria-label="Paginação das entregas" className="flex items-center justify-between gap-3">
          <p className="text-apoio text-texto-suave">
            Página {ganhos.pagina} de {totalPaginas} · {ganhos.qtdEntregas}{' '}
            {ganhos.qtdEntregas === 1 ? 'entrega' : 'entregas'}
          </p>

          <div className="flex gap-2">
            <Botao
              variante="secundario"
              disabled={ganhos.pagina <= 1}
              onClick={() => setPagina((p) => Math.max(1, p - 1))}
            >
              Anterior
            </Botao>
            <Botao
              variante="secundario"
              disabled={ganhos.pagina >= totalPaginas}
              onClick={() => setPagina((p) => p + 1)}
            >
              Próxima
            </Botao>
          </div>
        </nav>
      )}
    </div>
  )
}
