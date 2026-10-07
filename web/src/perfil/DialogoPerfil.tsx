import { useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { AvatarUsuario } from '@/components/AvatarUsuario'
import { Botao } from '@/components/Botao'
import { Dialogo } from '@/components/Dialogo'
import { Etiqueta } from '@/components/Etiqueta'
import { IconeX } from '@/components/icones/IconeX'
import type { EnderecoResolvido } from '@/dominio/endereco'
import { api } from '@/lib/api'
import { MapaConfirmacaoEndereco } from '@/perfil/MapaConfirmacaoEndereco'
import { usePerfil } from '@/perfil/contexto'
import type { NovoEnderecoDaLoja } from '@/perfil/contexto'
import { reduzirImagem } from '@/perfil/reduzirImagem'

const ROTULO_DO_PAPEL: Record<string, string> = {
  AdministradorSistema: 'Administrador',
  DonoRestaurante: 'Restaurante',
  Entregador: 'Motoboy',
}

const CLASSE_CAMPO =
  'w-full rounded-controle border border-borda-forte bg-superficie px-3 py-2 text-apoio text-texto outline-offset-2 focus-visible:outline-2 focus-visible:outline-marca-600'

function Linha({ rotulo, valor }: { rotulo: string; valor: ReactNode }) {
  return (
    <div className="flex items-baseline justify-between gap-4 border-b border-borda py-2 last:border-b-0">
      <span className="text-rotulo uppercase text-texto-suave">{rotulo}</span>
      <span className="truncate text-corpo text-texto">{valor}</span>
    </div>
  )
}

// Linha que vira formulário no clique em "Editar". Em repouso é idêntica à
// Linha de leitura — a edição não pode custar espaço numa janela que é, antes
// de tudo, um resumo.
function LinhaEditavel({
  rotulo,
  valor,
  tipo = 'text',
  onSalvar,
}: {
  rotulo: string
  valor: string
  tipo?: 'text' | 'email'
  onSalvar: (novo: string) => Promise<string | null>
}) {
  const [editando, setEditando] = useState(false)
  const [texto, setTexto] = useState(valor)
  const [salvando, setSalvando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)

  function abrir() {
    setTexto(valor)
    setErro(null)
    setEditando(true)
  }

  async function salvar() {
    setSalvando(true)
    const falha = await onSalvar(texto.trim())
    setSalvando(false)

    if (falha) {
      // Mantém o que foi digitado: quase sempre falta corrigir um caractere.
      setErro(falha)
      return
    }

    setEditando(false)
  }

  return (
    <div className="flex flex-col gap-2 border-b border-borda py-2 last:border-b-0">
      <div className="flex items-baseline justify-between gap-4">
        <span className="text-rotulo uppercase text-texto-suave">{rotulo}</span>
        {editando ? (
          <Botao variante="sutil" tamanho="pequeno" onClick={() => setEditando(false)} disabled={salvando}>
            Cancelar
          </Botao>
        ) : (
          <span className="flex min-w-0 items-baseline gap-2">
            <span className="truncate text-corpo text-texto">{valor}</span>
            <Botao variante="sutil" tamanho="pequeno" onClick={abrir}>
              Editar
            </Botao>
          </span>
        )}
      </div>

      {editando && (
        <form
          onSubmit={(e) => {
            e.preventDefault()
            void salvar()
          }}
          className="flex items-center gap-2"
        >
          <input
            type={tipo}
            value={texto}
            onChange={(e) => {
              setTexto(e.target.value)
              setErro(null)
            }}
            disabled={salvando}
            autoFocus
            className={CLASSE_CAMPO}
          />
          <Botao type="submit" tamanho="pequeno" carregando={salvando} disabled={texto.trim().length === 0}>
            Salvar
          </Botao>
        </form>
      )}

      {erro && <p className="text-apoio text-perigo">{erro}</p>}
    </div>
  )
}

// Endereço é o único que precisa de mais de um campo: o servidor monta
// logradouro/bairro/cidade e a coordenada a partir do CEP, então no caminho
// normal aqui só entram CEP, número e complemento.
function LinhaEndereco({
  valor,
  onSalvar,
}: {
  valor: string
  onSalvar: (novo: NovoEnderecoDaLoja) => Promise<string | null>
}) {
  const [editando, setEditando] = useState(false)
  const [cep, setCep] = useState('')
  const [numero, setNumero] = useState('')
  const [complemento, setComplemento] = useState('')
  const [achado, setAchado] = useState<EnderecoResolvido | null>(null)
  // Ajuste feito no mapa (arrasto ou busca por lugar) — vence o que o CEP
  // resolveu sozinho. É o que garante precisão mesmo quando nenhuma fonte de
  // geocodificação tem o prédio exato: quem confirma por último é a pessoa
  // olhando o mapa, não o serviço de terceiro.
  const [coordenadaAjustada, setCoordenadaAjustada] = useState<{ lat: number; lng: number } | null>(
    null,
  )
  const [buscando, setBuscando] = useState(false)
  const [salvando, setSalvando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)

  const coordenadaFinal =
    coordenadaAjustada
    ?? (achado?.latitude != null && achado?.longitude != null
      ? { lat: achado.latitude, lng: achado.longitude }
      : null)

  function abrir() {
    setCep('')
    setNumero('')
    setComplemento('')
    setAchado(null)
    setCoordenadaAjustada(null)
    setErro(null)
    setEditando(true)
  }

  // Busca ao completar os 8 dígitos, não a cada tecla: consulta a serviço de
  // terceiro não deve disparar enquanto a pessoa ainda está digitando.
  async function aoMudarCep(valor: string) {
    setCep(valor)
    setAchado(null)
    setCoordenadaAjustada(null)
    setErro(null)

    const digitos = valor.replace(/\D/g, '')
    if (digitos.length !== 8) return

    await buscar(digitos, numero)
  }

  async function buscar(digitos: string, numeroAtual: string) {
    setBuscando(true)

    try {
      const resultado = await api.get<EnderecoResolvido>(
        `/pedidos-internos/cep/${digitos}${numeroAtual.trim() ? `?numero=${encodeURIComponent(numeroAtual.trim())}` : ''}`,
      )
      setAchado(resultado)
      // CEP novo: qualquer ajuste de pino de uma busca anterior não vale mais.
      setCoordenadaAjustada(null)
    } catch {
      setErro('CEP não encontrado.')
    } finally {
      setBuscando(false)
    }
  }

  // Refaz a busca ao sair do número: com ele a geocodificação tenta o número
  // predial antes de cair no CEP, e o ponto sai mais perto da porta.
  function aoSairDoNumero() {
    const digitos = cep.replace(/\D/g, '')
    if (digitos.length === 8 && numero.trim()) void buscar(digitos, numero)
  }

  async function salvar() {
    setSalvando(true)
    const falha = await onSalvar({
      cep: cep.trim(),
      numero: numero.trim(),
      complemento: complemento.trim(),
      latitude: coordenadaFinal?.lat ?? null,
      longitude: coordenadaFinal?.lng ?? null,
    })
    setSalvando(false)

    if (falha) {
      setErro(falha)
      return
    }

    setEditando(false)
  }

  return (
    <div className="flex flex-col gap-2 border-b border-borda py-2 last:border-b-0">
      <div className="flex items-baseline justify-between gap-4">
        <span className="text-rotulo uppercase text-texto-suave">Endereço</span>
        {editando ? (
          <Botao variante="sutil" tamanho="pequeno" onClick={() => setEditando(false)} disabled={salvando}>
            Cancelar
          </Botao>
        ) : (
          <span className="flex min-w-0 items-baseline gap-2">
            <span className="truncate text-corpo text-texto">{valor}</span>
            <Botao variante="sutil" tamanho="pequeno" onClick={abrir}>
              Editar
            </Botao>
          </span>
        )}
      </div>

      {editando && (
        <form
          onSubmit={(e) => {
            e.preventDefault()
            void salvar()
          }}
          className="flex flex-col gap-2"
        >
          <div className="flex gap-2">
            <input
              value={cep}
              onChange={(e) => void aoMudarCep(e.target.value)}
              placeholder="CEP"
              inputMode="numeric"
              disabled={salvando}
              autoFocus
              className={CLASSE_CAMPO}
            />
            <input
              value={numero}
              onChange={(e) => {
                setNumero(e.target.value)
                setErro(null)
              }}
              onBlur={aoSairDoNumero}
              placeholder="Número"
              disabled={salvando}
              className={CLASSE_CAMPO}
            />
          </div>

          {/* O que a busca encontrou, antes de salvar: é isso que transforma
              "não foi possível localizar" numa informação acionável. */}
          {buscando && <p className="text-apoio text-texto-suave">Buscando endereço…</p>}

          {achado && (
            <div className="flex flex-col gap-2">
              <div className="flex flex-col gap-1 rounded-controle border border-borda bg-superficie-alt px-3 py-2">
                <p className="text-apoio text-texto">
                  {achado.logradouro || 'Logradouro não informado'}
                  {achado.bairro ? ` — ${achado.bairro}` : ''}
                </p>
                <p className="text-apoio text-texto-suave">
                  {achado.cidade}
                  {achado.estado ? `/${achado.estado}` : ''}
                </p>
                {coordenadaAjustada ? (
                  <p className="text-apoio text-sucesso">
                    Posição ajustada manualmente no mapa ({coordenadaAjustada.lat.toFixed(6)},{' '}
                    {coordenadaAjustada.lng.toFixed(6)}).
                  </p>
                ) : achado.latitude !== null && achado.longitude !== null ? (
                  achado.coordenadaAproximada ? (
                    <p className="text-apoio text-alerta">
                      Localização aproximada pelo CEP — esta rua não está no OpenStreetMap, então o
                      ponto pode cair a algumas quadras. Arraste o pino abaixo até a porta certa.
                    </p>
                  ) : (
                    <p className="text-apoio text-sucesso">
                      Localizado no mapa ({achado.latitude.toFixed(6)}, {achado.longitude.toFixed(6)}
                      ). Confira abaixo e ajuste se precisar.
                    </p>
                  )
                ) : (
                  <p className="text-apoio text-alerta">
                    Endereço encontrado, mas sem ponto no mapa — esta rua não está no OpenStreetMap.
                    Arraste o pino abaixo até o local certo, ou busque pelo nome.
                  </p>
                )}
              </div>

              {/* Confirmação final: o pino nasce onde a geocodificação achou
                  (ou no centro do Brasil, se nada foi achado) e pode ser
                  arrastado ou reposicionado pela busca — é isto que garante
                  precisão mesmo quando a fonte automática erra ou não tem o
                  prédio. */}
              <MapaConfirmacaoEndereco
                latitude={coordenadaFinal?.lat ?? null}
                longitude={coordenadaFinal?.lng ?? null}
                onMudarPosicao={(lat, lng) => setCoordenadaAjustada({ lat, lng })}
              />
            </div>
          )}

          <div className="flex gap-2">
            <input
              value={complemento}
              onChange={(e) => setComplemento(e.target.value)}
              placeholder="Complemento (opcional)"
              disabled={salvando}
              className={CLASSE_CAMPO}
            />
            <Botao
              type="submit"
              tamanho="pequeno"
              carregando={salvando}
              disabled={
                cep.trim().length === 0
                || numero.trim().length === 0
                || buscando
                // Sem ponto no mapa o endereço não serve para o que ele existe:
                // ancorar o mapa da operação. Salvar aqui gravaria a loja sem
                // posição e o pin sumiria.
                || !coordenadaFinal
              }
            >
              Salvar
            </Botao>
          </div>
        </form>
      )}

      {erro && <p className="text-apoio text-perigo">{erro}</p>}
    </div>
  )
}

export function DialogoPerfil({ aberto, onFechar }: { aberto: boolean; onFechar: () => void }) {
  const { perfil, carregando, erro, enviarFoto, alterarEmail, alterarNomeDaLoja, alterarEndereco } =
    usePerfil()
  const entradaRef = useRef<HTMLInputElement>(null)
  const [enviando, setEnviando] = useState(false)
  const [erroDaFoto, setErroDaFoto] = useState<string | null>(null)

  async function aoEscolherArquivo(arquivo: File | undefined) {
    if (!arquivo) return

    setErroDaFoto(null)
    setEnviando(true)

    try {
      await enviarFoto(await reduzirImagem(arquivo))
    } catch {
      // A mensagem da API já vem no `erro` do hook; aqui só cobre a falha de
      // leitura do arquivo, que não passa pela API.
      setErroDaFoto((atual) => atual ?? 'Não foi possível ler esta imagem.')
    } finally {
      setEnviando(false)
      // Limpa para o mesmo arquivo poder ser escolhido de novo: sem isso o
      // input não dispara change na segunda tentativa.
      if (entradaRef.current) entradaRef.current.value = ''
    }
  }

  const data = perfil ? new Date(perfil.membroDesde).toLocaleDateString('pt-BR') : ''

  return (
    <Dialogo aberto={aberto} onFechar={onFechar} titulo="Perfil">
      <div className="relative flex flex-col gap-4 p-5">
        {/* Canto superior direito: é onde se procura o fechar de uma janela, e
            libera a base do diálogo para o conteúdo. */}
        <button
          type="button"
          onClick={onFechar}
          aria-label="Fechar"
          className="absolute right-3 top-3 inline-flex size-8 items-center justify-center rounded-controle text-texto-suave outline-offset-2 transition-colors hover:bg-superficie-alt hover:text-texto focus-visible:outline-2 focus-visible:outline-marca-600"
        >
          <IconeX className="size-5" />
        </button>

        <div className="flex items-center gap-4 pr-8">
          {/* A própria foto é o botão de trocar: é onde a pessoa clica por
              instinto, e um botão separado ao lado seria alvo duplicado. */}
          <button
            type="button"
            onClick={() => entradaRef.current?.click()}
            disabled={enviando}
            className="group relative rounded-full outline-offset-2 focus-visible:outline-2 focus-visible:outline-marca-600"
          >
            <AvatarUsuario
              nome={perfil?.nome ?? ''}
              foto={perfil?.fotoBase64}
              className="size-16 text-titulo"
            />
            <span className="absolute inset-0 flex items-center justify-center rounded-full bg-texto/60 text-rotulo text-texto-invertido opacity-0 transition-opacity group-hover:opacity-100 group-focus-visible:opacity-100">
              {enviando ? 'Enviando…' : 'Trocar'}
            </span>
          </button>

          <input
            ref={entradaRef}
            type="file"
            accept="image/png,image/jpeg,image/webp"
            className="sr-only"
            onChange={(e) => void aoEscolherArquivo(e.target.files?.[0])}
          />

          <div className="flex min-w-0 flex-col gap-1">
            <p className="truncate text-titulo text-texto">{perfil?.nome ?? '—'}</p>
            {perfil && <Etiqueta>{ROTULO_DO_PAPEL[perfil.papel] ?? perfil.papel}</Etiqueta>}
          </div>
        </div>

        {carregando && <p className="text-apoio text-texto-suave">Carregando…</p>}
        {(erro || erroDaFoto) && <p className="text-apoio text-perigo">{erro ?? erroDaFoto}</p>}

        {perfil && (
          <div className="flex flex-col">
            <LinhaEditavel rotulo="E-mail" valor={perfil.email} tipo="email" onSalvar={alterarEmail} />
            <Linha rotulo="Na ROTEVIA desde" valor={data} />

            {perfil.loja && (
              <>
                <LinhaEditavel rotulo="Loja" valor={perfil.loja.nome} onSalvar={alterarNomeDaLoja} />
                <LinhaEndereco
                  valor={perfil.loja.endereco ?? 'Não informado'}
                  onSalvar={alterarEndereco}
                />
              </>
            )}

            {perfil.entregador && (
              <>
                <Linha rotulo="CPF" valor={perfil.entregador.cpf} />
                <Linha rotulo="Telefone" valor={perfil.entregador.telefone} />
                <Linha rotulo="Moto" valor={perfil.entregador.modeloDaMoto} />
                <Linha rotulo="Placa" valor={perfil.entregador.placa} />
                <Linha
                  rotulo="Disponível"
                  valor={perfil.entregador.disponivelParaEntrega ? 'Sim' : 'Não'}
                />
              </>
            )}
          </div>
        )}
      </div>
    </Dialogo>
  )
}
