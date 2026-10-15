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

// Dois passos em sequência: /catalogo/status confirma o escopo e devolve o
// groupId, /catalogo/ifood traz categorias e itens. Separar os dois endpoints
// no backend permite que o painel diga "o módulo está liberado mas o cardápio
// ainda está sincronizando" em vez de uma mensagem única de falha.
function PainelIFood({ merchantId }: { merchantId: string | null }) {
  const [estado, setEstado] = useState<
    'ocioso' | 'verificando' | 'carregando-itens' | 'pronto' | 'bloqueado' | 'vazio'
  >('ocioso')
  const [diagnostico, setDiagnostico] = useState<DiagnosticoCatalogoIFood | null>(null)
  const [catalogo, setCatalogo] = useState<CatalogoCompletoIFood | null>(null)
  const [erro, setErro] = useState<string | null>(null)

  useEffect(() => {
    if (!merchantId) return
    let ativo = true

    async function carregar() {
      setEstado('verificando')
      setErro(null)
      setCatalogo(null)
      try {
        const status = await api.get<DiagnosticoCatalogoIFood>(
          `/restaurantes/${merchantId}/catalogo/status`,
        )
        if (!ativo) return
        setDiagnostico(status)

        if (status.quantidade === 0) {
          setEstado('vazio')
          return
        }

        setEstado('carregando-itens')
        const completo = await api.get<CatalogoCompletoIFood>(
          `/restaurantes/${merchantId}/catalogo/ifood`,
        )
        if (!ativo) return
        setCatalogo(completo)
        setEstado('pronto')
      } catch (e) {
        if (!ativo) return
        setErro(e instanceof ErroDaApi ? e.message : 'Não foi possível consultar o iFood.')
        setEstado('bloqueado')
      }
    }

    void carregar()
    return () => {
      ativo = false
    }
  }, [merchantId])

  if (!merchantId) {
    return <PainelIndisponivel rotulo="iFood" bloqueio="Sessão sem restaurante vinculado." />
  }

  if (estado === 'ocioso' || estado === 'verificando') {
    return (
      <Cartao>
        <CartaoCorpo>
          <p className="text-corpo text-texto-fraco">Consultando o catálogo no iFood…</p>
        </CartaoCorpo>
      </Cartao>
    )
  }

  if (estado === 'bloqueado') {
    return (
      <Cartao>
        <CartaoCorpo className="space-y-3">
          <h2 className="text-[15px] font-semibold text-texto">Catálogo ainda não disponível</h2>
          <p className="text-corpo leading-relaxed text-texto-fraco">{erro}</p>
          <p className="text-corpo leading-relaxed text-texto-fraco">
            Enquanto o módulo não é habilitado, o cardápio segue sendo gerido
            direto no portal do iFood.
          </p>
          <Botao variante="secundario" tamanho="pequeno" onClick={() => window.location.reload()}>
            Verificar novamente
          </Botao>
        </CartaoCorpo>
      </Cartao>
    )
  }

  if (estado === 'vazio') {
    return (
      <Cartao>
        <CartaoCorpo className="space-y-3">
          <h2 className="text-[15px] font-semibold text-texto">Nenhum catálogo publicado</h2>
          <p className="text-corpo leading-relaxed text-texto-fraco">
            A integração está liberada, mas a loja ainda não tem catálogo
            publicado no iFood. Publique um pelo portal e o cardápio aparece aqui.
          </p>
        </CartaoCorpo>
      </Cartao>
    )
  }

  if (estado === 'carregando-itens') {
    return (
      <Cartao>
        <CartaoCorpo className="space-y-2">
          <p className="text-corpo text-texto-fraco">
            Carregando categorias e itens do iFood…
          </p>
          {diagnostico && (
            <p className="font-mono text-rotulo text-texto-mudo">
              catálogo {diagnostico.catalogos[0]?.catalogId ?? '—'}
            </p>
          )}
        </CartaoCorpo>
      </Cartao>
    )
  }

  if (!catalogo) {
    // Estado defensivo: 'pronto' sem catalogo não deveria acontecer. Mostrar
    // mensagem genérica em vez de quebrar a tela.
    return (
      <Cartao>
        <CartaoCorpo>
          <p className="text-corpo text-texto-fraco">Catálogo indisponível.</p>
        </CartaoCorpo>
      </Cartao>
    )
  }

  return <PainelCatalogoIFood catalogo={catalogo} />
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
