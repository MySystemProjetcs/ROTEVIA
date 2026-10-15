import { useEffect, useState } from 'react'
import { Botao } from '@/components/Botao'
import { Cartao, CartaoCorpo } from '@/components/Cartao'
import { useSessao } from '@/auth/SessaoProvider'
import { ErroDaApi, api } from '@/lib/api'
import { cn } from '@/lib/cn'
import { Painel99Food } from './Painel99Food'
import { CANAIS_CATALOGO } from './tipos'
import type {
  CanalCatalogo,
  CatalogoCompletoIFood,
  DiagnosticoCatalogoIFood,
  ItemCatalogoIFood,
} from './tipos'

export function PaginaCatalogo() {
  const { usuario } = useSessao()
  const [canalAtivo, setCanalAtivo] = useState<CanalCatalogo>('ifood')

  const info = CANAIS_CATALOGO.find((c) => c.canal === canalAtivo) ?? CANAIS_CATALOGO[0]

  return (
    <div className="flex min-h-full flex-col gap-5">
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <p className="font-mono text-rotulo uppercase tracking-wide text-texto-mudo">
            Gestão · Canais de venda
          </p>
          <h1 className="mt-1 text-2xl font-bold tracking-tight text-texto">Catálogo</h1>
          <p className="mt-1 max-w-xl text-corpo text-texto-fraco">
            Cada plataforma tem o próprio catálogo. Itens, preços e
            disponibilidade são sincronizados por canal.
          </p>
        </div>
      </header>

      <nav className="flex items-center gap-1 border-b border-borda" aria-label="Canais de venda">
        {CANAIS_CATALOGO.map((c) => (
          <button
            key={c.canal}
            type="button"
            onClick={() => setCanalAtivo(c.canal)}
            className={cn(
              'relative inline-flex items-center gap-2 rounded-t-controle px-4 py-3 text-corpo transition-colors',
              c.canal === canalAtivo
                ? 'bg-marca-600/10 text-marca-300 hover:bg-marca-600/20'
                : 'text-texto-mudo hover:bg-marca-600/5 hover:text-marca-300',
            )}
            aria-current={c.canal === canalAtivo}
          >
            <span className="font-medium">{c.rotulo}</span>
            {!c.disponivel && (
              <span className="rounded-full border border-borda bg-superficie-alt px-2 py-0.5 text-rotulo text-texto-mudo">
                em breve
              </span>
            )}
            {c.canal === canalAtivo && (
              <span className="absolute inset-x-2 -bottom-px h-0.5 rounded-full bg-marca-600" aria-hidden />
            )}
          </button>
        ))}
      </nav>

      <div className="grid min-h-0 flex-1 grid-cols-1 gap-4 lg:grid-cols-[280px_1fr]">
        <aside className="flex flex-col gap-3">
          <Cartao>
            <CartaoCorpo className="space-y-2">
              <p className="font-mono text-rotulo uppercase text-texto-mudo">
                Catálogo do {info.rotulo}
              </p>
              <p className="text-corpo text-texto-fraco">
                {info.bloqueio
                  ?? 'Itens, categorias e disponibilidade sincronizados com a plataforma.'}
              </p>
            </CartaoCorpo>
          </Cartao>
        </aside>

        <section className="min-w-0">
          {canalAtivo === 'ifood' ? (
            <PainelIFood merchantId={usuario?.merchantId ?? null} />
          ) : canalAtivo === 'noventaenove' ? (
            <Painel99Food merchantId={usuario?.merchantId ?? null} />
          ) : (
            <PainelIndisponivel rotulo={info.rotulo} bloqueio={info.bloqueio} />
          )}
        </section>
      </div>
    </div>
  )
}

interface FalhaCatalogo {
  status: number | null
  titulo: string
  detalhe: string | null
}

function paraFalha(e: unknown): FalhaCatalogo {
  if (e instanceof ErroDaApi) {
    return {
      status: e.status,
      titulo: e.message,
      detalhe: e.codigo === 'erro.desconhecido' ? null : e.codigo,
    }
  }
  return {
    status: null,
    titulo: 'Não foi possível falar com a API.',
    detalhe: e instanceof Error ? e.message : null,
  }
}

// Dois passos com resultado independente: primeiro os catálogos da loja
// (GET /merchants/{merchantId}/catalogs no iFood), depois os itens. Se os
// itens falharem, o id do catálogo continua na tela.
function PainelIFood({ merchantId }: { merchantId: string | null }) {
  const [etapa, setEtapa] = useState<'catalogos' | 'itens' | 'concluido'>('catalogos')
  const [diagnostico, setDiagnostico] = useState<DiagnosticoCatalogoIFood | null>(null)
  const [falhaCatalogos, setFalhaCatalogos] = useState<FalhaCatalogo | null>(null)
  const [catalogo, setCatalogo] = useState<CatalogoCompletoIFood | null>(null)
  const [falhaItens, setFalhaItens] = useState<FalhaCatalogo | null>(null)
  const [tentativa, setTentativa] = useState(0)

  useEffect(() => {
    if (!merchantId) return
    let ativo = true

    async function carregar() {
      setEtapa('catalogos')
      setDiagnostico(null)
      setFalhaCatalogos(null)
      setCatalogo(null)
      setFalhaItens(null)

      let status: DiagnosticoCatalogoIFood
      try {
        status = await api.get<DiagnosticoCatalogoIFood>(`/restaurantes/${merchantId}/catalogo/status`)
      } catch (e) {
        if (!ativo) return
        setFalhaCatalogos(paraFalha(e))
        setEtapa('concluido')
        return
      }
      if (!ativo) return
      setDiagnostico(status)

      if (status.catalogos.length === 0) {
        setEtapa('concluido')
        return
      }

      setEtapa('itens')
      try {
        const completo = await api.get<CatalogoCompletoIFood>(`/restaurantes/${merchantId}/catalogo/ifood`)
        if (!ativo) return
        setCatalogo(completo)
      } catch (e) {
        if (!ativo) return
        setFalhaItens(paraFalha(e))
      } finally {
        if (ativo) setEtapa('concluido')
      }
    }

    void carregar()
    return () => {
      ativo = false
    }
  }, [merchantId, tentativa])

  if (!merchantId) {
    return <PainelIndisponivel rotulo="iFood" bloqueio="Sessão sem restaurante vinculado." />
  }

  const tentarNovamente = () => setTentativa((t) => t + 1)

  if (etapa === 'catalogos') {
    return <CartaoCarregando texto="Consultando os catálogos da loja no iFood…" />
  }

  if (falhaCatalogos) {
    return <CartaoFalha falha={falhaCatalogos} aoTentarNovamente={tentarNovamente} />
  }

  return (
    <div className="flex min-h-0 flex-col gap-4">
      {diagnostico && <CartaoCatalogosIFood diagnostico={diagnostico} />}

      {etapa === 'itens' && <CartaoCarregando texto="Carregando categorias e itens do catálogo…" />}
      {falhaItens && <CartaoFalha falha={falhaItens} aoTentarNovamente={tentarNovamente} />}
      {catalogo && <PainelCatalogoIFood catalogo={catalogo} />}
    </div>
  )
}

const FORMATO_DATA = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' })

function CartaoCatalogosIFood({ diagnostico }: { diagnostico: DiagnosticoCatalogoIFood }) {
  return (
    <Cartao>
      <CartaoCorpo className="space-y-3">
        <div className="space-y-1">
          <h2 className="text-[15px] font-semibold text-texto">
            {diagnostico.quantidade === 1
              ? '1 catálogo retornado pelo iFood'
              : `${diagnostico.quantidade} catálogos retornados pelo iFood`}
          </h2>
          <p className="break-all font-mono text-rotulo text-texto-mudo">
            loja no iFood: {diagnostico.ifoodMerchantId}
          </p>
        </div>

        {diagnostico.catalogos.length === 0 ? (
          <p className="text-corpo text-texto-fraco">
            O iFood não devolveu nenhum catálogo para esta loja.
          </p>
        ) : (
          <ul className="divide-y divide-borda">
            {diagnostico.catalogos.map((c) => (
              <li key={c.catalogId} className="space-y-1.5 py-3">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <p className="text-corpo text-texto">{c.context.join(' · ') || 'Sem contexto'}</p>
                  <span
                    className={cn(
                      'shrink-0 rounded-full border px-2 py-0.5 text-rotulo',
                      c.status === 'AVAILABLE'
                        ? 'border-sucesso/30 bg-sucesso-fundo text-sucesso'
                        : 'border-borda bg-superficie-alt text-texto-mudo',
                    )}
                  >
                    {c.status ?? 'sem status'}
                  </span>
                </div>
                <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-0.5 font-mono text-rotulo">
                  <dt className="text-texto-mudo">catalogId</dt>
                  <dd className="select-all break-all text-texto">{c.catalogId}</dd>
                  <dt className="text-texto-mudo">groupId</dt>
                  <dd className="select-all break-all text-texto">{c.groupId ?? '—'}</dd>
                  <dt className="text-texto-mudo">alterado</dt>
                  <dd className="text-texto">
                    {c.modifiedAt ? FORMATO_DATA.format(new Date(c.modifiedAt)) : '—'}
                  </dd>
                </dl>
              </li>
            ))}
          </ul>
        )}
      </CartaoCorpo>
    </Cartao>
  )
}

function CartaoCarregando({ texto }: { texto: string }) {
  return (
    <Cartao>
      <CartaoCorpo>
        <p className="text-corpo text-texto-fraco">{texto}</p>
      </CartaoCorpo>
    </Cartao>
  )
}

function CartaoFalha({
  falha,
  aoTentarNovamente,
}: {
  falha: FalhaCatalogo
  aoTentarNovamente: () => void
}) {
  return (
    <Cartao>
      <CartaoCorpo className="space-y-3">
        <h2 className="text-[15px] font-semibold text-texto">{falha.titulo}</h2>
        {falha.detalhe && (
          <p className="break-words font-mono text-rotulo text-texto-mudo">{falha.detalhe}</p>
        )}
        {falha.status === 403 && (
          <p className="text-corpo leading-relaxed text-texto-fraco">
            Enquanto o módulo não é habilitado, o cardápio segue sendo gerido
            direto no portal do iFood.
          </p>
        )}
        <Botao variante="secundario" tamanho="pequeno" onClick={aoTentarNovamente}>
          Tentar novamente
        </Botao>
      </CartaoCorpo>
    </Cartao>
  )
}

function PainelCatalogoIFood({ catalogo }: { catalogo: CatalogoCompletoIFood }) {
  if (catalogo.categorias.length === 0) {
    return (
      <Cartao>
        <CartaoCorpo className="space-y-2">
          <h2 className="text-[15px] font-semibold text-texto">Catálogo vazio</h2>
          <p className="text-corpo leading-relaxed text-texto-fraco">
            O catálogo está publicado mas não tem itens vendáveis agora. Itens
            pausados, com violação ou fora da agenda não entram nessa listagem.
          </p>
        </CartaoCorpo>
      </Cartao>
    )
  }

  return (
    <div className="flex min-h-0 flex-col gap-4">
      <Cartao>
        <CartaoCorpo className="flex flex-wrap items-baseline justify-between gap-2">
          <h2 className="text-[15px] font-semibold text-texto">
            {catalogo.totalItens === 1
              ? '1 item publicado'
              : `${catalogo.totalItens} itens publicados`}
            {' em '}
            {catalogo.categorias.length === 1
              ? '1 categoria'
              : `${catalogo.categorias.length} categorias`}
          </h2>
          <p className="font-mono text-rotulo text-texto-mudo">
            catálogo {catalogo.catalogId.slice(0, 8)}…
          </p>
        </CartaoCorpo>
      </Cartao>

      <div className="flex flex-col gap-3">
        {catalogo.categorias.map((cat) => (
          <Cartao key={cat.categoryId}>
            <CartaoCorpo className="space-y-3">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <h3 className="text-corpo font-semibold text-texto">{cat.nome}</h3>
                <div className="flex items-center gap-2">
                  {cat.pausada && (
                    <span className="rounded-full border border-alerta/30 bg-alerta-fundo px-2 py-0.5 text-rotulo text-alerta">
                      pausada
                    </span>
                  )}
                  <span className="font-mono text-rotulo text-texto-mudo">
                    {cat.itens.length} {cat.itens.length === 1 ? 'item' : 'itens'}
                  </span>
                </div>
              </div>

              {cat.itens.length === 0 ? (
                <p className="text-corpo text-texto-mudo">Nenhum item vendável nesta categoria.</p>
              ) : (
                <ul className="divide-y divide-borda">
                  {cat.itens.map((item) => (
                    <LinhaItemIFood key={item.itemId} item={item} />
                  ))}
                </ul>
              )}
            </CartaoCorpo>
          </Cartao>
        ))}
      </div>
    </div>
  )
}

const FORMATO_REAL = new Intl.NumberFormat('pt-BR', {
  style: 'currency',
  currency: 'BRL',
})

function LinhaItemIFood({ item }: { item: ItemCatalogoIFood }) {
  return (
    <li className="flex items-start justify-between gap-3 py-3">
      <div className="min-w-0 flex-1">
        <p className="truncate text-corpo text-texto">{item.nome}</p>
        {item.descricao && (
          <p className="mt-0.5 line-clamp-2 text-rotulo text-texto-fraco">
            {item.descricao}
          </p>
        )}
        {item.restricoes.length > 0 && (
          <p className="mt-1 text-rotulo text-alerta">
            {item.restricoes.join(' · ')}
          </p>
        )}
      </div>
      <div className="shrink-0 text-right">
        {item.precoOriginal && item.precoOriginal > item.preco ? (
          <>
            <p className="text-rotulo text-texto-mudo line-through">
              {FORMATO_REAL.format(item.precoOriginal)}
            </p>
            <p className="text-corpo font-semibold text-marca-300">
              {FORMATO_REAL.format(item.preco)}
            </p>
          </>
        ) : (
          <p className="text-corpo font-semibold text-texto">
            {FORMATO_REAL.format(item.preco)}
          </p>
        )}
      </div>
    </li>
  )
}

function PainelIndisponivel({ rotulo, bloqueio }: { rotulo: string; bloqueio: string | null }) {
  return (
    <Cartao>
      <CartaoCorpo className="space-y-3">
        <h2 className="text-[15px] font-semibold text-texto">
          Catálogo do {rotulo} ainda não conectado
        </h2>
        <p className="text-corpo leading-relaxed text-texto-fraco">{bloqueio}</p>
      </CartaoCorpo>
    </Cartao>
  )
}
