import { useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { Botao } from '@/components/Botao'
import { Campo } from '@/components/Campo'
import { Cartao, CartaoCorpo, CartaoCabecalho } from '@/components/Cartao'
import { AvatarIniciais } from '@/components/AvatarIniciais'
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

function CartaoKpi({
  rotulo,
  valor,
  cartao,
  numero,
}: {
  rotulo: string
  valor: number
  cartao: string
  numero: string
}) {
  return (
    <div className={cn('rounded-cartao border p-4 shadow-cartao', cartao)}>
      <p className="text-rotulo uppercase text-texto-suave">{rotulo}</p>
      <p className={cn('text-destaque tabular-nums', numero)}>{valor}</p>
    </div>
  )
}

export function PaginaMotoboys() {
  const { entregadores, carregando, erro, convidar } = useMotoboys()
  const { pedidos } = usePedidos()
  const [formAberto, setFormAberto] = useState(false)
  const [form, setForm] = useState<NovoEntregador>(VAZIO)
  const [enviando, setEnviando] = useState(false)
  const [erroForm, setErroForm] = useState<string | null>(null)
  const [ultimoConvite, setUltimoConvite] = useState<EntregadorConvidado | null>(null)

  const emEntrega = useMemo(
    () =>
      new Set(
        pedidos
          .filter((p) => p.entregadorId && EM_ENTREGA.includes(p.status))
          .map((p) => p.entregadorId as string),
      ),
    [pedidos],
  )

  const disponiveis = entregadores.filter((e) => situacaoDe(e, emEntrega) === 'Disponivel').length
  const emEntregaQtd = entregadores.filter((e) => situacaoDe(e, emEntrega) === 'EmEntrega').length
  const offline = entregadores.length - disponiveis - emEntregaQtd

  function abrirFormulario() {
    setUltimoConvite(null)
    setErroForm(null)
    setForm(VAZIO)
    setFormAberto(true)
  }

  function fecharFormulario() {
    setFormAberto(false)
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
      <div className="flex flex-col gap-3 xl:flex-row xl:items-stretch">
        <div className="grid flex-1 grid-cols-2 gap-3 xl:grid-cols-4">
          <CartaoKpi
            rotulo="Disponíveis"
            valor={disponiveis}
            cartao="border-sucesso/30 bg-sucesso-fundo"
            numero="text-sucesso"
          />
          <CartaoKpi
            rotulo="Em entrega"
            valor={emEntregaQtd}
            cartao="border-marca-200 bg-marca-50"
            numero="text-marca-700"
          />
          <CartaoKpi
            rotulo="Offline"
            valor={offline}
            cartao="border-borda bg-superficie-afundada"
            numero="text-texto"
          />
          <CartaoKpi
            rotulo="Total"
            valor={entregadores.length}
            cartao="border-borda bg-superficie"
            numero="text-texto"
          />
        </div>

        {!formAberto && (
          <Botao variante="acaoConfirmar" className="xl:w-44" onClick={abrirFormulario}>
            + Cadastrar
          </Botao>
        )}
      </div>

      {erro && (
        <Cartao className="border-perigo">
          <CartaoCorpo>{erro}</CartaoCorpo>
        </Cartao>
      )}

      {formAberto && (
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
                <Botao type="button" variante="secundario" onClick={fecharFormulario}>
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
            </tr>
          </thead>
          <tbody className="divide-y divide-borda">
            {carregando ? (
              <tr>
                <td colSpan={4} className="px-4 py-6 text-apoio text-texto-suave">Carregando...</td>
              </tr>
            ) : entregadores.length === 0 ? (
              <tr>
                <td colSpan={4} className="px-4 py-6 text-apoio text-texto-suave">
                  Nenhum motoboy cadastrado ainda.
                </td>
              </tr>
            ) : (
              entregadores.map((entregador) => {
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
                  </tr>
                )
              })
            )}
          </tbody>
        </table>
      </div>
    </div>
  )
}
