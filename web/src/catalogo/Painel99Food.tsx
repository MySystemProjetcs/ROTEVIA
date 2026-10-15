import { useCallback, useEffect, useState } from 'react'
import { Cartao, CartaoCorpo } from '@/components/Cartao'
import { Switch } from '@/components/Switch'
import { ErroDaApi, api } from '@/lib/api'
import { cn } from '@/lib/cn'
import type { Cardapio99, Categoria99, ItemCardapio, ResultadoDisponibilidade } from './tipos'

const MOEDA = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })

export function Painel99Food({ merchantId }: { merchantId: string | null }) {
  const [cardapio, setCardapio] = useState<Cardapio99 | null>(null)
  const [carregando, setCarregando] = useState(true)
  const [erro, setErro] = useState<string | null>(null)
  // Itens com alteração em voo. O switch some do alcance do dedo se a linha
  // inteira ficar desabilitada, então só o item alterado trava.
  const [salvando, setSalvando] = useState<ReadonlySet<string>>(new Set())

  const carregar = useCallback(async () => {
    if (!merchantId) return
    setCarregando(true)
    setErro(null)
    try {
      setCardapio(await api.get<Cardapio99>(`/restaurantes/${merchantId}/catalogo/99food`))
    } catch (e) {
      setErro(e instanceof ErroDaApi ? e.message : 'Não foi possível carregar o cardápio.')
      setCardapio(null)
    } finally {
      setCarregando(false)
    }
  }, [merchantId])

  useEffect(() => {
    void carregar()
  }, [carregar])

  async function alternar(item: ItemCardapio, disponivel: boolean) {
    if (!merchantId) return

    setSalvando((atual) => new Set(atual).add(item.appItemId))
    // Otimista: o switch responde na hora. A 99 pode recusar, e aí voltamos —
    // esperar o round-trip faria o toggle parecer travado.
    aplicarLocal(item.appItemId, disponivel)

    try {
      const resultado = await api.put<ResultadoDisponibilidade>(
        `/restaurantes/${merchantId}/catalogo/99food/disponibilidade`,
        { appItemIds: [item.appItemId], disponivel },
      )

      // A 99 aceita lote parcialmente: estar em `falha` significa que o item
      // continuou como estava, mesmo a chamada tendo dado 200.
      if (resultado.falha.includes(item.appItemId)) {
        aplicarLocal(item.appItemId, !disponivel)
        setErro(`A 99Food recusou a alteração de "${item.nome}".`)
      }
    } catch (e) {
      aplicarLocal(item.appItemId, !disponivel)
      setErro(e instanceof ErroDaApi ? e.message : 'Não foi possível alterar a disponibilidade.')
    } finally {
      setSalvando((atual) => {
        const proximo = new Set(atual)
        proximo.delete(item.appItemId)
        return proximo
      })
    }
  }

  function aplicarLocal(appItemId: string, disponivel: boolean) {
    setCardapio((atual) => {
      if (!atual) return atual
      const trocar = (i: ItemCardapio) => (i.appItemId === appItemId ? { ...i, disponivel } : i)
      return {
        ...atual,
        categorias: atual.categorias.map((c) => ({ ...c, itens: c.itens.map(trocar) })),
        itensSemCategoria: atual.itensSemCategoria.map(trocar),
      }
    })
  }

  if (!merchantId) {
    return <Aviso titulo="Sessão sem restaurante" texto="Entre com uma conta vinculada a uma loja." />
  }

  if (carregando) {
    return <Aviso titulo="Carregando cardápio…" texto="Consultando a 99Food." />
  }

  if (erro && !cardapio) {
    return <Aviso titulo="Cardápio indisponível" texto={erro} />
  }

  if (!cardapio || cardapio.totalItens === 0) {
    return (
      <Aviso
        titulo="Nenhum item publicado"
        texto="Esta loja ainda não tem itens no cardápio da 99Food."
      />
    )
  }

  return (
    <div className="space-y-4">
      {erro && (
        <Cartao>
          <CartaoCorpo>
            <p className="text-corpo text-alerta">{erro}</p>
          </CartaoCorpo>
        </Cartao>
      )}

      <p className="text-corpo text-texto-fraco">
        {cardapio.totalItens === 1 ? '1 item publicado' : `${cardapio.totalItens} itens publicados`}
      </p>

      {cardapio.categorias.map((categoria) => (
        <Grupo
          key={categoria.appCategoryId}
          titulo={categoria.nome}
          itens={categoria.itens}
          salvando={salvando}
          onAlternar={alternar}
        />
      ))}

      {cardapio.itensSemCategoria.length > 0 && (
        <Grupo
          titulo="Sem categoria"
          itens={cardapio.itensSemCategoria}
          salvando={salvando}
          onAlternar={alternar}
        />
      )}
    </div>
  )
}

function Grupo({
  titulo,
  itens,
  salvando,
  onAlternar,
}: {
  titulo: string
  itens: ItemCardapio[]
  salvando: ReadonlySet<string>
  onAlternar: (item: ItemCardapio, disponivel: boolean) => void
}) {
  if (itens.length === 0) return null

  return (
    <Cartao>
      <CartaoCorpo className="space-y-1">
        <p className="font-mono text-rotulo uppercase text-texto-mudo">{titulo}</p>

        <ul className="divide-y divide-borda">
          {itens.map((item) => (
            <li key={item.appItemId} className="flex items-center justify-between gap-3 py-3">
              <div className="min-w-0 flex-1">
                <p
                  className={cn(
                    'truncate text-corpo',
                    item.disponivel ? 'text-texto' : 'text-texto-mudo line-through',
                  )}
                >
                  {item.nome}
                </p>
                {item.descricao && (
                  <p className="truncate text-rotulo text-texto-mudo">{item.descricao}</p>
                )}
              </div>

              <span className="shrink-0 font-mono text-corpo text-texto-fraco">
                {MOEDA.format(item.preco)}
              </span>

              <Switch
                marcado={item.disponivel}
                onMudar={(marcado) => onAlternar(item, marcado)}
                rotulo={`${item.nome} disponível na 99Food`}
                disabled={salvando.has(item.appItemId)}
              />
            </li>
          ))}
        </ul>
      </CartaoCorpo>
    </Cartao>
  )
}

function Aviso({ titulo, texto }: { titulo: string; texto: string }) {
  return (
    <Cartao>
      <CartaoCorpo className="space-y-2">
        <h2 className="text-[15px] font-semibold text-texto">{titulo}</h2>
        <p className="text-corpo leading-relaxed text-texto-fraco">{texto}</p>
      </CartaoCorpo>
    </Cartao>
  )
}
