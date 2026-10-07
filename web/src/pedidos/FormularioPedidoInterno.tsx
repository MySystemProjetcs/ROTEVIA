import { useState } from 'react'
import type { FormEvent } from 'react'
import { Botao } from '@/components/Botao'
import { Cartao, CartaoCorpo, CartaoRodape } from '@/components/Cartao'
import { Menu } from '@/components/Menu'
import type { OpcaoDeMenu } from '@/components/Menu'
import { Switch } from '@/components/Switch'
import type { EnderecoResolvido } from '@/dominio/endereco'
import { api, ErroDaApi } from '@/lib/api'
import { formatarDinheiro } from '@/lib/tempo'

interface ItemDigitado {
  nome: string
  quantidade: string
  precoUnitario: string
}

const CLASSE_CAMPO =
  'h-toque w-full rounded-controle border border-borda-forte bg-superficie px-3 text-corpo text-texto outline-offset-2 focus-visible:outline-2 focus-visible:outline-marca-600'

const CLASSE_ROTULO = 'text-rotulo uppercase text-texto-suave'

const ITEM_VAZIO: ItemDigitado = { nome: '', quantidade: '1', precoUnitario: '' }

type FormaDePagamento = 'Dinheiro' | 'Pix' | 'Credito' | 'Debito' | 'Voucher'

const FORMAS: readonly OpcaoDeMenu<FormaDePagamento>[] = [
  { valor: 'Dinheiro', rotulo: 'Dinheiro' },
  { valor: 'Pix', rotulo: 'PIX' },
  { valor: 'Credito', rotulo: 'Crédito' },
  { valor: 'Debito', rotulo: 'Débito' },
  { valor: 'Voucher', rotulo: 'Voucher' },
]

// Vírgula é o separador decimal que o lojista digita; Number() só entende ponto.
function paraNumero(valor: string): number {
  const limpo = valor.replace(/\./g, '').replace(',', '.')
  const numero = Number(limpo)

  return Number.isFinite(numero) ? numero : 0
}

export function FormularioPedidoInterno({ onLancado }: { onLancado: () => void }) {
  const [clienteNome, setClienteNome] = useState('')
  const [clienteTelefone, setClienteTelefone] = useState('')
  const [cep, setCep] = useState('')
  const [numero, setNumero] = useState('')
  const [complemento, setComplemento] = useState('')
  const [formaPagamento, setFormaPagamento] = useState<FormaDePagamento>('Dinheiro')
  // Falso = o motoboy cobra na entrega. Dinheiro quase sempre é cobrar, então
  // é o padrão de quem lança venda de balcão por telefone.
  const [jaPago, setJaPago] = useState(false)
  const [itens, setItens] = useState<ItemDigitado[]>([{ ...ITEM_VAZIO }])
  const [endereco, setEndereco] = useState<EnderecoResolvido | null>(null)
  const [buscandoCep, setBuscandoCep] = useState(false)
  const [salvando, setSalvando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)

  // O que o cliente paga são os itens. A taxa da entrega não entra aqui: quem
  // define quanto o motoboy recebe é a regra global da loja, não o lançamento.
  const total = itens.reduce(
    (soma, item) => soma + paraNumero(item.precoUnitario) * paraNumero(item.quantidade),
    0,
  )

  // Busca ao completar os 8 dígitos, não a cada tecla: consulta a serviço de
  // terceiro não deve disparar enquanto a pessoa ainda está digitando.
  async function aoMudarCep(valor: string) {
    setCep(valor)
    setEndereco(null)

    const digitos = valor.replace(/\D/g, '')
    if (digitos.length !== 8) return

    setBuscandoCep(true)
    setErro(null)

    try {
      setEndereco(await api.get<EnderecoResolvido>(`/pedidos-internos/cep/${digitos}`))
    } catch {
      setErro('CEP não encontrado.')
    } finally {
      setBuscandoCep(false)
    }
  }

  function alterarItem(indice: number, campo: keyof ItemDigitado, valor: string) {
    setItens((atual) => atual.map((item, i) => (i === indice ? { ...item, [campo]: valor } : item)))
  }

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault()
    setSalvando(true)
    setErro(null)

    try {
      await api.post('/pedidos-internos', {
        clienteNome,
        clienteTelefone: clienteTelefone || null,
        cep: cep.replace(/\D/g, ''),
        numero,
        complemento: complemento || null,
        referencia: null,
        formaPagamento,
        jaPago,
        itens: itens
          .filter((i) => i.nome.trim() !== '')
          .map((i) => ({
            nome: i.nome,
            quantidade: Math.trunc(paraNumero(i.quantidade)),
            precoUnitario: paraNumero(i.precoUnitario),
            observacoes: null,
          })),
      })

      onLancado()
    } catch (e) {
      setErro(e instanceof ErroDaApi ? e.message : 'Não foi possível lançar o pedido.')
    } finally {
      setSalvando(false)
    }
  }

  return (
    <Cartao>
      <form onSubmit={aoEnviar}>
        <CartaoCorpo className="flex flex-col gap-4">
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="flex flex-col gap-1">
              <label htmlFor="pi-cliente" className={CLASSE_ROTULO}>Cliente</label>
              <input
                id="pi-cliente"
                required
                value={clienteNome}
                onChange={(e) => setClienteNome(e.target.value)}
                className={CLASSE_CAMPO}
              />
            </div>

            <div className="flex flex-col gap-1">
              <label htmlFor="pi-telefone" className={CLASSE_ROTULO}>Telefone</label>
              <input
                id="pi-telefone"
                inputMode="tel"
                value={clienteTelefone}
                onChange={(e) => setClienteTelefone(e.target.value)}
                className={CLASSE_CAMPO}
              />
            </div>
          </div>

          <div className="grid gap-3 sm:grid-cols-3">
            <div className="flex flex-col gap-1">
              <label htmlFor="pi-cep" className={CLASSE_ROTULO}>CEP</label>
              <input
                id="pi-cep"
                required
                inputMode="numeric"
                placeholder="00000-000"
                value={cep}
                onChange={(e) => void aoMudarCep(e.target.value)}
                className={CLASSE_CAMPO}
              />
            </div>

            <div className="flex flex-col gap-1">
              <label htmlFor="pi-numero" className={CLASSE_ROTULO}>Número</label>
              <input
                id="pi-numero"
                required
                value={numero}
                onChange={(e) => setNumero(e.target.value)}
                className={CLASSE_CAMPO}
              />
            </div>

            <div className="flex flex-col gap-1">
              <label htmlFor="pi-complemento" className={CLASSE_ROTULO}>Complemento</label>
              <input
                id="pi-complemento"
                value={complemento}
                onChange={(e) => setComplemento(e.target.value)}
                className={CLASSE_CAMPO}
              />
            </div>
          </div>

          {buscandoCep && <p className="text-apoio text-texto-suave">Buscando endereço…</p>}

          {endereco && (
            <p className="rounded-controle bg-superficie-afundada px-3 py-2 text-apoio text-texto-suave">
              {endereco.logradouro}, {endereco.bairro} — {endereco.cidade}/{endereco.estado}
              {endereco.latitude === null && (
                // Sem coordenada o pedido existe, mas não aparece no mapa.
                <span className="text-alerta"> · sem localização no mapa</span>
              )}
            </p>
          )}

          <div className="flex flex-col gap-2">
            <span className={CLASSE_ROTULO}>Itens</span>

            {itens.map((item, indice) => (
              <div key={indice} className="grid gap-2 sm:grid-cols-[1fr_5rem_7rem_auto]">
                <input
                  aria-label={`Nome do item ${indice + 1}`}
                  placeholder="Descrição"
                  value={item.nome}
                  onChange={(e) => alterarItem(indice, 'nome', e.target.value)}
                  className={CLASSE_CAMPO}
                />
                <input
                  aria-label={`Quantidade do item ${indice + 1}`}
                  inputMode="numeric"
                  value={item.quantidade}
                  onChange={(e) => alterarItem(indice, 'quantidade', e.target.value)}
                  className={CLASSE_CAMPO}
                />
                <input
                  aria-label={`Preço do item ${indice + 1}`}
                  inputMode="decimal"
                  placeholder="0,00"
                  value={item.precoUnitario}
                  onChange={(e) => alterarItem(indice, 'precoUnitario', e.target.value)}
                  className={CLASSE_CAMPO}
                />
                <Botao
                  type="button"
                  variante="sutil"
                  // O último item não some: sem nenhuma linha não há o que pedir.
                  disabled={itens.length === 1}
                  onClick={() => setItens((atual) => atual.filter((_, i) => i !== indice))}
                >
                  Remover
                </Botao>
              </div>
            ))}

            <Botao
              type="button"
              variante="secundario"
              onClick={() => setItens((atual) => [...atual, { ...ITEM_VAZIO }])}
            >
              Adicionar item
            </Botao>
          </div>

          {/* Total, forma e confirmação numa linha só: fechar o pedido é uma
              decisão única — quanto é, como paga e se já pagou. */}
          <div className="grid gap-3 sm:grid-cols-3">
            <div className="flex flex-col justify-end gap-1">
              <span className={CLASSE_ROTULO}>Total</span>
              {/* Mesma altura dos controles ao lado para os quatro campos
                  ficarem na mesma linha de base. */}
              <p className="flex h-toque items-center text-destaque tabular-nums text-texto">
                {formatarDinheiro(total)}
              </p>
            </div>

            <div className="flex flex-col gap-1">
              <span className={CLASSE_ROTULO}>Forma de pagamento</span>
              <Menu
                rotulo="Forma de pagamento"
                valor={formaPagamento}
                opcoes={FORMAS}
                onEscolher={setFormaPagamento}
              />
            </div>

            <div className="flex flex-col justify-end gap-1">
              <span className={CLASSE_ROTULO}>Confirmação</span>
              <div className="flex h-toque items-center">
                <Switch
                  marcado={jaPago}
                  onMudar={setJaPago}
                  rotulo={jaPago ? 'Já pago' : 'Cobrar na entrega'}
                  // Verde quando pago — o mesmo tom que a etiqueta do cartão usa
                  // para dizer que não há mais nada a receber.
                  classeLigado="bg-sucesso"
                />
              </div>
            </div>
          </div>

          <p className="text-apoio text-texto-suave">
            {jaPago
              ? 'Não aparece para o motoboy cobrar.'
              : `O motoboy recebe ${formatarDinheiro(total)} na entrega.`}
          </p>

          {erro && <p className="text-apoio text-perigo">{erro}</p>}
        </CartaoCorpo>

        <CartaoRodape>
          <Botao type="submit" larguraTotal carregando={salvando}>
            Lançar pedido
          </Botao>
        </CartaoRodape>
      </form>
    </Cartao>
  )
}
