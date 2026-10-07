import { useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { Botao } from '@/components/Botao'
import { Campo } from '@/components/Campo'
import { Cartao, CartaoCorpo, CartaoCabecalho } from '@/components/Cartao'
import { Dialogo } from '@/components/Dialogo'
import { MenuDeAcoes } from '@/components/MenuDeAcoes'
import { AvatarIniciais } from '@/components/AvatarIniciais'
import { IconeBusca } from '@/components/icones/IconeBusca'
import { MenuHorizontal } from '@/components/MenuHorizontal'
import { CartaoTaxaPorEntrega } from '@/dashboard/CartaoTaxaPorEntrega'
import type { Entregador, EntregadorConvidado, NovoEntregador } from '@/dominio/entregador'
import { STATUS_EM_ENTREGA } from '@/dominio/pedido'
import { ErroDaApi } from '@/lib/api'
import { cn } from '@/lib/cn'
import { formatarTelefoneExibicao } from '@/lib/telefone'
import { usePedidos } from '@/pedidos/usePedidos'
import { useMotoboys } from './useMotoboys'

const VAZIO: NovoEntregador = { cpf: '', nome: '', telefone: '', modeloDaMoto: '', placa: '', email: '' }

// Alocado em entrega ativa (aguardando aceite incluso): é o que separa "Em
// Entrega" de "Disponível". Derivado dos pedidos — o vínculo sozinho só diz
// Convidado/Ativo/Suspenso.
const EM_ENTREGA = STATUS_EM_ENTREGA

type Situacao = 'Disponivel' | 'EmEntrega' | 'Offline'

function situacaoDe(entregador: Entregador, emEntrega: Set<string>): Situacao {
  if (entregador.status !== 'Ativo' || !entregador.disponivel) return 'Offline'
  return emEntrega.has(entregador.courierId) ? 'EmEntrega' : 'Disponivel'
}

// Rótulo da coluna STATUS. Convidado mostra o vínculo real (alerta); o resto
// mostra a situação operacional. Classes literais para o Tailwind gerar.
function pillDoStatus(entregador: Entregador, situacao: Situacao): { texto: string; classe: string } {
  if (entregador.status === 'Convidado')
    return { texto: 'Convidado', classe: 'border-alerta/40 bg-alerta-fundo text-alerta' }

  if (situacao === 'EmEntrega')
    return { texto: 'Em Entrega', classe: 'border-marca-300 bg-marca-50 text-marca-700' }

  if (situacao === 'Disponivel')
    return { texto: 'Disponível', classe: 'border-sucesso/30 bg-sucesso-fundo text-sucesso' }

  return { texto: 'Offline', classe: 'border-borda-forte bg-superficie-afundada text-texto-suave' }
}

function avatarDaSituacao(situacao: Situacao): string {
  if (situacao === 'EmEntrega') return 'border-marca-300 bg-marca-50 text-marca-700'
  if (situacao === 'Disponivel') return 'border-sucesso/30 bg-sucesso-fundo text-sucesso'
  return 'border-borda-forte bg-superficie-afundada text-texto-suave'
}

// Um painel por vez: taxa e formulário de cadastro dividem o mesmo espaço
// abaixo do menu, e nenhum aparece quando não foi pedido — a lista de
// motoboys fica em primeiro plano por padrão.
type PainelAtivo = 'nenhum' | 'taxa' | 'cadastrar'

// Remove acento e caixa alta pra que "joao" case com "João".
function normalizar(texto: string): string {
  return texto.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase()
}

export function PaginaMotoboys() {
  const { entregadores, carregando, erro, convidar, atualizar, remover } = useMotoboys()
  const { pedidos } = usePedidos()
  const [painel, setPainel] = useState<PainelAtivo>('nenhum')
  const [busca, setBusca] = useState('')
  const [form, setForm] = useState<NovoEntregador>(VAZIO)
  const [enviando, setEnviando] = useState(false)
  const [erroForm, setErroForm] = useState<string | null>(null)
  const [ultimoConvite, setUltimoConvite] = useState<EntregadorConvidado | null>(null)

  // Ações da linha: edição abre diálogo; remoção pede confirmação na própria
  // linha (dois cliques) — apagar vínculo não pode acontecer por clique torto.
  const [editando, setEditando] = useState<Entregador | null>(null)
  const [confirmandoRemocao, setConfirmandoRemocao] = useState<string | null>(null)
  const [removendo, setRemovendo] = useState(false)
  const [erroAcao, setErroAcao] = useState<string | null>(null)

  async function removerVinculo(entregador: Entregador) {
    setRemovendo(true)
    const falha = await remover(entregador.linkId)
    setRemovendo(false)
    setConfirmandoRemocao(null)
    setErroAcao(falha)
  }

  const emEntrega = useMemo(
    () =>
      new Set(
        pedidos
          .filter((p) => p.entregadorId && EM_ENTREGA.includes(p.status))
          .map((p) => p.entregadorId as string),
      ),
    [pedidos],
  )

  // Filtro de nome com normalização — casa acento, caixa e substring.
  const buscaNormalizada = normalizar(busca.trim())
  const entregadoresVisiveis = useMemo(
    () =>
      buscaNormalizada
        ? entregadores.filter((e) => normalizar(e.nome).includes(buscaNormalizada))
        : entregadores,
    [entregadores, buscaNormalizada],
  )

  function abrirCadastrar() {
    setUltimoConvite(null)
    setErroForm(null)
    setForm(VAZIO)
    setPainel(painel === 'cadastrar' ? 'nenhum' : 'cadastrar')
  }

  function abrirTaxa() {
    setPainel(painel === 'taxa' ? 'nenhum' : 'taxa')
  }

  function fecharPainel() {
    setPainel('nenhum')
  }

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault()
    setErroForm(null)
    setEnviando(true)

    try {
      const resultado = await convidar(form)
      setUltimoConvite(resultado)
      setForm(VAZIO)
    } catch (e) {
      setErroForm(e instanceof ErroDaApi ? e.message : 'Não foi possível gerar o convite.')
    } finally {
      setEnviando(false)
    }
  }

  function campo(chave: keyof NovoEntregador) {
    return {
      value: form[chave],
      onChange: (e: React.ChangeEvent<HTMLInputElement>) => setForm((atual) => ({ ...atual, [chave]: e.target.value })),
    }
  }

  return (
    <div className="flex flex-col gap-4">
      {/* Menu horizontal (padrão do react-admin HorizontalMenu, reimplementado
          local — CLAUDE.md §10) com as ações da página. Ativo destaca por
          fundo pra ler como pill selecionada. À direita, na mesma linha, um
          campo pra filtrar a lista abaixo pelo nome — encolhe primeiro em
          tela estreita. */}
      <MenuHorizontal
        itens={[
          { chave: 'taxa',      rotulo: 'Taxa por entrega', ativo: painel === 'taxa',      aoClicar: abrirTaxa },
          { chave: 'cadastrar', rotulo: '+ Cadastrar',      ativo: painel === 'cadastrar', aoClicar: abrirCadastrar },
        ]}
      >
        <label className="flex min-w-0 items-center gap-2 rounded-[10px] border border-borda bg-superficie px-2.5 py-1.5">
          <IconeBusca className="size-3.5 shrink-0 text-texto-fraco" />
          <input
            type="search"
            value={busca}
            onChange={(e) => setBusca(e.target.value)}
            placeholder="Buscar motoboy"
            className="w-40 min-w-0 bg-transparent text-[13px] text-texto placeholder:text-texto-mudo outline-none sm:w-56"
          />
        </label>
      </MenuHorizontal>

      {erro && (
        <Cartao className="border-perigo">
          <CartaoCorpo>{erro}</CartaoCorpo>
        </Cartao>
      )}

      {painel === 'taxa' && <CartaoTaxaPorEntrega />}

      {painel === 'cadastrar' && (
        <Cartao elevacao="elevada">
          <CartaoCabecalho>
            <h2 className="text-corpo font-semibold text-texto">Novo motoboy</h2>
          </CartaoCabecalho>
          <CartaoCorpo>
            <form onSubmit={aoEnviar} className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <Campo rotulo="Nome completo" required {...campo('nome')} />
              <Campo rotulo="CPF" required maxLength={11} placeholder="Somente números" {...campo('cpf')} />
              <Campo rotulo="Modelo da moto" required {...campo('modeloDaMoto')} />
              <Campo rotulo="Placa" required {...campo('placa')} />
              <Campo rotulo="E-mail" type="email" required {...campo('email')} />
              <Campo
                rotulo="WhatsApp"
                required
                placeholder="DDD + número, ex.: 11999998888"
                apoio="Sem +55 — a gente completa sozinho."
                {...campo('telefone')}
              />

              {erroForm && <p className="text-apoio text-perigo sm:col-span-2">{erroForm}</p>}

              <div className="flex gap-3 sm:col-span-2">
                <Botao type="submit" carregando={enviando}>
                  Gerar convite
                </Botao>
                <Botao type="button" variante="secundario" onClick={fecharPainel}>
                  Cancelar
                </Botao>
              </div>
            </form>

            {ultimoConvite && (
              <div className="mt-4 rounded-controle bg-superficie-alt p-3">
                <p className="text-apoio font-medium text-texto">
                  {ultimoConvite.enviadoPeloWhatsApp
                    ? 'Convite enviado pelo WhatsApp do motoboy.'
                    : 'Convite gerado, mas não deu pra enviar pelo WhatsApp agora — compartilhe o link manualmente:'}
                </p>
                {!ultimoConvite.enviadoPeloWhatsApp && (
                  <p className="mt-1 break-all text-apoio text-marca-600">{ultimoConvite.conviteUrl}</p>
                )}
              </div>
            )}
          </CartaoCorpo>
        </Cartao>
      )}

      <div className="overflow-x-auto rounded-cartao border border-borda bg-superficie shadow-cartao">
        <table className="w-full min-w-[720px] border-collapse text-left">
          <thead>
            <tr className="border-b border-borda">
              <th scope="col" className="px-4 py-3 text-rotulo uppercase text-texto-fraco">Motoboy</th>
              <th scope="col" className="px-4 py-3 text-rotulo uppercase text-texto-fraco">Telefone</th>
              <th scope="col" className="px-4 py-3 text-rotulo uppercase text-texto-fraco">Veículo / Placa</th>
              <th scope="col" className="px-4 py-3 text-rotulo uppercase text-texto-fraco">Status</th>
              <th scope="col" className="px-4 py-3 text-right text-rotulo uppercase text-texto-fraco">Ações</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-borda">
            {carregando ? (
              <tr>
                <td colSpan={5} className="px-4 py-6 text-apoio text-texto-suave">Carregando...</td>
              </tr>
            ) : entregadores.length === 0 ? (
              <tr>
                <td colSpan={5} className="px-4 py-6 text-apoio text-texto-suave">
                  Nenhum motoboy cadastrado ainda.
                </td>
              </tr>
            ) : entregadoresVisiveis.length === 0 ? (
              <tr>
                <td colSpan={5} className="px-4 py-6 text-apoio text-texto-suave">
                  Nenhum motoboy corresponde a “{busca}”.
                </td>
              </tr>
            ) : (
              entregadoresVisiveis.map((entregador) => {
                const situacao = situacaoDe(entregador, emEntrega)
                const pill = pillDoStatus(entregador, situacao)

                return (
                  <tr key={entregador.linkId}>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-3">
                        <AvatarIniciais nome={entregador.nome} className={avatarDaSituacao(situacao)} />
                        <span className="text-corpo font-semibold text-texto">{entregador.nome}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-corpo text-texto tabular-nums">
                      {formatarTelefoneExibicao(entregador.telefone)}
                    </td>
                    <td className="px-4 py-3">
                      <p className="text-corpo font-medium text-texto">{entregador.modeloDaMoto}</p>
                      <p className="text-apoio uppercase text-texto-fraco">{entregador.placa}</p>
                    </td>
                    <td className="px-4 py-3">
                      <span
                        className={cn(
                          'inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-apoio font-medium',
                          pill.classe,
                        )}
                      >
                        <span aria-hidden className="size-1.5 rounded-full bg-current" />
                        {pill.texto}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center justify-end gap-2">
                        {confirmandoRemocao === entregador.linkId ? (
                          <>
                            <span className="text-apoio text-texto-suave">
                              {entregador.status === 'Convidado' ? 'Revogar convite?' : 'Remover da loja?'}
                            </span>
                            <Botao
                              variante="perigo"
                              tamanho="pequeno"
                              carregando={removendo}
                              onClick={() => void removerVinculo(entregador)}
                            >
                              Confirmar
                            </Botao>
                            <Botao
                              variante="secundario"
                              tamanho="pequeno"
                              disabled={removendo}
                              onClick={() => setConfirmandoRemocao(null)}
                            >
                              Cancelar
                            </Botao>
                          </>
                        ) : (
                          <MenuDeAcoes
                            rotulo={`Ações de ${entregador.nome}`}
                            acoes={[
                              {
                                chave: 'editar',
                                rotulo: 'Editar cadastro',
                                aoEscolher: () => {
                                  setErroAcao(null)
                                  setEditando(entregador)
                                },
                              },
                              {
                                chave: 'remover',
                                rotulo:
                                  entregador.status === 'Convidado' ? 'Revogar convite' : 'Remover da loja',
                                perigosa: true,
                                aoEscolher: () => {
                                  setErroAcao(null)
                                  setConfirmandoRemocao(entregador.linkId)
                                },
                              },
                            ]}
                          />
                        )}
                      </div>
                    </td>
                  </tr>
                )
              })
            )}
          </tbody>
        </table>
      </div>

      {erroAcao && (
        <Cartao className="border-perigo">
          <CartaoCorpo>{erroAcao}</CartaoCorpo>
        </Cartao>
      )}

      {editando && (
        <DialogoEditarEntregador
          entregador={editando}
          onFechar={() => setEditando(null)}
          onSalvar={(dados) => atualizar(editando.linkId, dados)}
        />
      )}
    </div>
  )
}

// Edição do cadastro. CPF fica de fora: é a chave natural que identifica o
// mesmo motoboy entre lojas (CLAUDE.md §6) e não pode mudar.
function DialogoEditarEntregador({
  entregador,
  onFechar,
  onSalvar,
}: {
  entregador: Entregador
  onFechar: () => void
  onSalvar: (dados: {
    nome: string
    telefone: string
    modeloDaMoto: string
    placa: string
  }) => Promise<string | null>
}) {
  const [nome, setNome] = useState(entregador.nome)
  const [telefone, setTelefone] = useState(entregador.telefone)
  const [modeloDaMoto, setModeloDaMoto] = useState(entregador.modeloDaMoto)
  const [placa, setPlaca] = useState(entregador.placa)
  const [salvando, setSalvando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)

  async function enviar(evento: FormEvent) {
    evento.preventDefault()
    setSalvando(true)
    const falha = await onSalvar({ nome, telefone, modeloDaMoto, placa })
    setSalvando(false)

    if (falha) {
      setErro(falha)
      return
    }

    onFechar()
  }

  return (
    <Dialogo aberto onFechar={onFechar} titulo="Editar motoboy">
      <form onSubmit={enviar} className="flex flex-col gap-4 p-5">
        <p className="text-apoio text-texto-suave">
          O cadastro do motoboy é único no sistema: a correção vale para todas as lojas em que ele
          trabalha. O CPF não muda.
        </p>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Campo rotulo="Nome completo" required value={nome} onChange={(e) => setNome(e.target.value)} />
          <Campo
            rotulo="WhatsApp"
            required
            apoio="DDD + número, ex.: 11999998888"
            value={telefone}
            onChange={(e) => setTelefone(e.target.value)}
          />
          <Campo
            rotulo="Modelo da moto"
            required
            value={modeloDaMoto}
            onChange={(e) => setModeloDaMoto(e.target.value)}
          />
          <Campo rotulo="Placa" required value={placa} onChange={(e) => setPlaca(e.target.value)} />
        </div>

        {erro && <p className="text-apoio text-perigo">{erro}</p>}

        <div className="flex gap-3">
          <Botao type="submit" carregando={salvando}>
            Salvar
          </Botao>
          <Botao type="button" variante="secundario" onClick={onFechar} disabled={salvando}>
            Cancelar
          </Botao>
        </div>
      </form>
    </Dialogo>
  )
}
