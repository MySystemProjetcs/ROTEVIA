import { useEffect, useState } from 'react'
import { Botao } from '@/components/Botao'
import { Cartao, CartaoCorpo } from '@/components/Cartao'
import { useSessao } from '@/auth/SessaoProvider'
import { ErroDaApi, api } from '@/lib/api'
import { cn } from '@/lib/cn'
import { CANAIS_CATALOGO } from './tipos'
import type { CanalCatalogo, DiagnosticoCatalogoIFood } from './tipos'

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
                {info.bloqueio ?? 'Sincronizado com a plataforma.'}
              </p>
            </CartaoCorpo>
          </Cartao>
        </aside>

        <section className="min-w-0">
          {canalAtivo === 'ifood' ? (
            <PainelIFood merchantId={usuario?.merchantId ?? null} />
          ) : (
            <PainelIndisponivel rotulo={info.rotulo} bloqueio={info.bloqueio} />
          )}
        </section>
      </div>
    </div>
  )
}

// Única aba com backend por trás hoje: consulta o escopo de catálogo do app
// no iFood. Existe para o lojista descobrir sozinho se o módulo já foi
// liberado, em vez de abrir chamado a cada tentativa.
function PainelIFood({ merchantId }: { merchantId: string | null }) {
  const [estado, setEstado] = useState<'ocioso' | 'consultando' | 'liberado' | 'bloqueado'>('ocioso')
  const [dados, setDados] = useState<DiagnosticoCatalogoIFood | null>(null)
  const [erro, setErro] = useState<string | null>(null)

  // Consulta uma vez ao abrir a aba: o estado do escopo muda no portal do
  // iFood, fora daqui, então mostrar o último valor conhecido sem conferir
  // seria mentira.
  useEffect(() => {
    if (!merchantId) return
    let ativo = true

    async function consultar() {
      setEstado('consultando')
      setErro(null)
      try {
        const resposta = await api.get<DiagnosticoCatalogoIFood>(
          `/restaurantes/${merchantId}/catalogo/status`,
        )
        if (!ativo) return
        setDados(resposta)
        setEstado('liberado')
      } catch (e) {
        if (!ativo) return
        setErro(e instanceof ErroDaApi ? e.message : 'Não foi possível consultar o iFood.')
        setEstado('bloqueado')
      }
    }

    void consultar()
    return () => {
      ativo = false
    }
  }, [merchantId])

  if (!merchantId) {
    return <PainelIndisponivel rotulo="iFood" bloqueio="Sessão sem restaurante vinculado." />
  }

  if (estado === 'consultando' || estado === 'ocioso') {
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

  // Escopo liberado: a partir daqui entram as listas de categorias e itens,
  // conforme os endpoints forem implementados. Por ora o painel confirma o
  // acesso e mostra o que a loja tem publicado.
  return (
    <Cartao>
      <CartaoCorpo className="space-y-3">
        <h2 className="text-[15px] font-semibold text-texto">
          {dados?.quantidade === 1
            ? '1 catálogo publicado'
            : `${dados?.quantidade ?? 0} catálogos publicados`}
        </h2>

        <ul className="divide-y divide-borda">
          {dados?.catalogos.map((c) => (
            <li key={c.catalogId} className="flex flex-wrap items-center justify-between gap-2 py-2.5">
              <div className="min-w-0">
                <p className="truncate text-corpo text-texto">{c.context.join(' · ') || 'Sem contexto'}</p>
                <p className="truncate font-mono text-rotulo text-texto-mudo">{c.catalogId}</p>
              </div>
              <span
                className={cn(
                  'shrink-0 rounded-full border px-2 py-0.5 text-rotulo',
                  c.status === 'AVAILABLE'
                    ? 'border-sucesso/30 bg-sucesso-fundo text-sucesso'
                    : 'border-borda bg-superficie-alt text-texto-mudo',
                )}
              >
                {c.status ?? 'desconhecido'}
              </span>
            </li>
          ))}
        </ul>
      </CartaoCorpo>
    </Cartao>
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
