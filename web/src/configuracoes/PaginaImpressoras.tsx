import { useCallback, useEffect, useState } from 'react'
import { api, ErroDaApi } from '@/lib/api'
import { useSessao } from '@/auth/SessaoProvider'
import { imprimirComandaPdf } from '@/lib/impressao/clientePdf'
import type { Comanda } from '@/lib/impressao'

type ModeloImpressora =
  | 'EscPosGenerica'
  | 'ElginI7'
  | 'ElginI8'
  | 'ElginI9'
  | 'EpsonTmT20'
  | 'BematechMp4200'
  | 'DarumaDr800'

type LarguraPapel = 'Oitenta' | 'CinquentaEOito'
type SetorDeImpressao = 'Cozinha' | 'Balcao' | 'Bar' | 'Expedicao'

interface Impressora {
  id: string
  nome: string
  modelo: ModeloImpressora
  largura: LarguraPapel
  setor: SetorDeImpressao
  imprimirAutomaticamente: boolean
  ativa: boolean
  criadaEm: string
}

interface Formulario {
  nome: string
  modelo: ModeloImpressora
  largura: LarguraPapel
  setor: SetorDeImpressao
  imprimirAutomaticamente: boolean
}

const MODELOS: Array<{ valor: ModeloImpressora; rotulo: string }> = [
  { valor: 'EscPosGenerica', rotulo: 'ESC/POS genérica' },
  { valor: 'ElginI9', rotulo: 'Elgin i9' },
  { valor: 'ElginI8', rotulo: 'Elgin i8' },
  { valor: 'ElginI7', rotulo: 'Elgin i7' },
  { valor: 'EpsonTmT20', rotulo: 'Epson TM-T20' },
  { valor: 'BematechMp4200', rotulo: 'Bematech MP-4200' },
  { valor: 'DarumaDr800', rotulo: 'Daruma DR-800' },
]

const SETORES: Array<{ valor: SetorDeImpressao; rotulo: string }> = [
  { valor: 'Cozinha', rotulo: 'Cozinha' },
  { valor: 'Balcao', rotulo: 'Balcão' },
  { valor: 'Bar', rotulo: 'Bar' },
  { valor: 'Expedicao', rotulo: 'Expedição' },
]

const VAZIO: Formulario = {
  nome: '',
  modelo: 'EscPosGenerica',
  largura: 'Oitenta',
  setor: 'Cozinha',
  imprimirAutomaticamente: true,
}

export function PaginaImpressoras() {
  const { usuario } = useSessao()
  const merchantId = usuario?.merchantId ?? null

  const [impressoras, setImpressoras] = useState<Impressora[]>([])
  const [carregando, setCarregando] = useState(true)
  const [erro, setErro] = useState<string | null>(null)
  const [editando, setEditando] = useState<string | null>(null)
  const [form, setForm] = useState<Formulario>(VAZIO)
  const [salvando, setSalvando] = useState(false)

  const recarregar = useCallback(async () => {
    if (!merchantId) return
    try {
      const lista = await api.get<Impressora[]>(`/restaurantes/${merchantId}/impressoras`)
      setImpressoras(lista)
      setErro(null)
    } catch (e) {
      setErro(e instanceof ErroDaApi ? e.message : 'Não foi possível carregar as impressoras.')
    } finally {
      setCarregando(false)
    }
  }, [merchantId])

  useEffect(() => {
    void recarregar()
  }, [recarregar])

  async function salvar(evento: React.FormEvent) {
    evento.preventDefault()
    if (!merchantId) return
    setSalvando(true)
    setErro(null)
    try {
      if (editando) {
        await api.put<Impressora>(`/restaurantes/${merchantId}/impressoras/${editando}`, form)
      } else {
        await api.post<Impressora>(`/restaurantes/${merchantId}/impressoras`, form)
      }
      setForm(VAZIO)
      setEditando(null)
      await recarregar()
    } catch (e) {
      setErro(e instanceof ErroDaApi ? e.message : 'Não foi possível salvar.')
    } finally {
      setSalvando(false)
    }
  }

  async function remover(id: string) {
    if (!merchantId) return
    if (!window.confirm('Remover esta impressora?')) return
    try {
      await api.del(`/restaurantes/${merchantId}/impressoras/${id}`)
      await recarregar()
    } catch (e) {
      setErro(e instanceof ErroDaApi ? e.message : 'Não foi possível remover.')
    }
  }

  function iniciarEdicao(x: Impressora) {
    setEditando(x.id)
    setForm({
      nome: x.nome,
      modelo: x.modelo,
      largura: x.largura,
      setor: x.setor,
      imprimirAutomaticamente: x.imprimirAutomaticamente,
    })
  }

  function cancelarEdicao() {
    setEditando(null)
    setForm(VAZIO)
  }

  function imprimirTeste() {
    const agora = new Date().toISOString()
    const comandaFake: Comanda = {
      pedidoId: 'teste-' + Math.random().toString(36).slice(2, 8),
      numeroExibicao: 'TST001',
      ehTeste: true,
      origem: 'Interno',
      criadoEm: agora,
      recebidoEm: agora,
      cliente: { nome: 'Cliente de Teste', telefone: '(11) 90000-0000', localizador: 'TESTE' },
      entrega: {
        logradouro: 'Rua da Impressora',
        numero: '80',
        complemento: null,
        bairro: 'Centro',
        cidade: 'São Paulo',
        estado: 'SP',
        cep: '01000-000',
        referencia: 'Comanda de teste',
      },
      itens: [
        { indice: 0, nome: 'X-Burger', quantidade: 1, unidade: null, precoUnitario: 25, precoTotal: 25, observacoes: 'Sem cebola' },
        { indice: 1, nome: 'Coca 350ml', quantidade: 2, unidade: null, precoUnitario: 7, precoTotal: 14, observacoes: null },
      ],
      pagamento: { descricao: 'Dinheiro', valorACobrar: 45, valorJaPago: 0 },
      valorTotal: 45,
      taxaEntrega: 6,
      codigoDeEntrega: '1234',
      exigeCodigoDeEntrega: true,
    }
    imprimirComandaPdf(comandaFake)
  }

  if (!merchantId) {
    return <div className="p-6 text-texto-suave">Selecione uma loja para gerenciar as impressoras.</div>
  }

  return (
    <div className="mx-auto flex max-w-4xl flex-col gap-6 p-6">
      <header className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-destaque text-texto">Impressoras</h1>
          <p className="text-corpo text-texto-suave">
            Cadastre as estações que receberão a comanda a cada pedido confirmado.
          </p>
        </div>
        <button
          type="button"
          onClick={imprimirTeste}
          className="rounded-md border border-borda bg-superficie px-3 py-2 text-rotulo hover:bg-superficie-elevada"
        >
          Imprimir comanda de teste
        </button>
      </header>

      {erro && (
        <div className="rounded-md border border-destaque-erro bg-destaque-erro/10 p-3 text-rotulo text-destaque-erro">
          {erro}
        </div>
      )}

      <form onSubmit={salvar} className="flex flex-col gap-4 rounded-cartao border border-borda bg-superficie p-5">
        <h2 className="text-titulo text-texto">
          {editando ? 'Editar impressora' : 'Nova impressora'}
        </h2>

        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          <label className="flex flex-col gap-1">
            <span className="text-rotulo text-texto-suave">Nome</span>
            <input
              type="text"
              required
              maxLength={60}
              value={form.nome}
              onChange={(e) => setForm((f) => ({ ...f, nome: e.target.value }))}
              placeholder="Ex.: Cozinha principal"
              className="rounded-md border border-borda bg-fundo p-2 text-corpo text-texto"
            />
          </label>

          <label className="flex flex-col gap-1">
            <span className="text-rotulo text-texto-suave">Setor</span>
            <select
              value={form.setor}
              onChange={(e) => setForm((f) => ({ ...f, setor: e.target.value as SetorDeImpressao }))}
              className="rounded-md border border-borda bg-fundo p-2 text-corpo text-texto"
            >
              {SETORES.map((s) => (
                <option key={s.valor} value={s.valor}>{s.rotulo}</option>
              ))}
            </select>
          </label>

          <label className="flex flex-col gap-1">
            <span className="text-rotulo text-texto-suave">Modelo</span>
            <select
              value={form.modelo}
              onChange={(e) => setForm((f) => ({ ...f, modelo: e.target.value as ModeloImpressora }))}
              className="rounded-md border border-borda bg-fundo p-2 text-corpo text-texto"
            >
              {MODELOS.map((m) => (
                <option key={m.valor} value={m.valor}>{m.rotulo}</option>
              ))}
            </select>
          </label>

          <label className="flex flex-col gap-1">
            <span className="text-rotulo text-texto-suave">Largura do papel</span>
            <select
              value={form.largura}
              onChange={(e) => setForm((f) => ({ ...f, largura: e.target.value as LarguraPapel }))}
              className="rounded-md border border-borda bg-fundo p-2 text-corpo text-texto"
            >
              <option value="Oitenta">80 mm (recomendado)</option>
              <option value="CinquentaEOito">58 mm</option>
            </select>
          </label>
        </div>

        <label className="inline-flex items-center gap-2">
          <input
            type="checkbox"
            checked={form.imprimirAutomaticamente}
            onChange={(e) => setForm((f) => ({ ...f, imprimirAutomaticamente: e.target.checked }))}
          />
          <span className="text-corpo text-texto">Imprimir automaticamente ao receber pedido</span>
        </label>

        <div className="flex items-center gap-3">
          <button
            type="submit"
            disabled={salvando}
            className="rounded-md bg-marca-500 px-4 py-2 text-corpo font-medium text-texto-contraste hover:bg-marca-400 disabled:opacity-50"
          >
            {salvando ? 'Salvando…' : editando ? 'Atualizar' : 'Cadastrar'}
          </button>
          {editando && (
            <button
              type="button"
              onClick={cancelarEdicao}
              className="rounded-md border border-borda px-4 py-2 text-corpo text-texto hover:bg-superficie-elevada"
            >
              Cancelar
            </button>
          )}
        </div>
      </form>

      <section className="flex flex-col gap-2">
        <h2 className="text-titulo text-texto">Cadastradas</h2>
        {carregando ? (
          <div className="text-texto-suave">Carregando…</div>
        ) : impressoras.length === 0 ? (
          <div className="rounded-md border border-dashed border-borda p-6 text-center text-texto-suave">
            Nenhuma impressora cadastrada ainda.
          </div>
        ) : (
          <ul className="flex flex-col gap-2">
            {impressoras.map((x) => (
              <li
                key={x.id}
                className="flex items-center justify-between gap-4 rounded-cartao border border-borda bg-superficie p-4"
              >
                <div className="flex flex-col">
                  <strong className="text-corpo text-texto">{x.nome}</strong>
                  <span className="text-rotulo text-texto-suave">
                    {MODELOS.find((m) => m.valor === x.modelo)?.rotulo} ·{' '}
                    {SETORES.find((s) => s.valor === x.setor)?.rotulo} ·{' '}
                    {x.largura === 'Oitenta' ? '80 mm' : '58 mm'}
                    {x.imprimirAutomaticamente ? ' · automática' : ' · manual'}
                  </span>
                </div>
                <div className="flex gap-2">
                  <button
                    type="button"
                    onClick={() => iniciarEdicao(x)}
                    className="rounded-md border border-borda px-3 py-1 text-rotulo text-texto hover:bg-superficie-elevada"
                  >
                    Editar
                  </button>
                  <button
                    type="button"
                    onClick={() => remover(x.id)}
                    className="rounded-md border border-destaque-erro px-3 py-1 text-rotulo text-destaque-erro hover:bg-destaque-erro/10"
                  >
                    Remover
                  </button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  )
}
