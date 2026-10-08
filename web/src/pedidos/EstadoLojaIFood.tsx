import { useEffect, useState } from 'react'
import { api } from '@/lib/api'

interface ContextoIFoodResponse {
  comerciante: unknown
  status: unknown
  atualizadoEm: string
  desatualizado: boolean
}

type EstadoDisponibilidade = 'carregando' | 'online' | 'offline' | 'indisponivel'

interface EstadoLoja {
  nome: string | null
  disponibilidade: EstadoDisponibilidade
  atualizadoEm: string | null
  desatualizado: boolean
}

const INTERVALO_ATUALIZACAO_MS = 5 * 60 * 1000

export function EstadoLojaIFood({
  merchantId,
  nomeFallback,
}: {
  merchantId: string | null
  nomeFallback: string | null
}) {
  const [loja, setLoja] = useState<EstadoLoja>({
    nome: nomeFallback,
    disponibilidade: 'carregando',
    atualizadoEm: null,
    desatualizado: false,
  })

  useEffect(() => {
    let ativo = true
    let timer: number | undefined

    if (!merchantId) return

    async function carregar() {
      try {
        const resposta = await api.get<ContextoIFoodResponse>('/restaurantes/ifood/contexto')
        if (!ativo) return

        setLoja({
          nome: obterNome(resposta.comerciante) ?? nomeFallback,
          disponibilidade: obterDisponibilidade(resposta.status),
          atualizadoEm: resposta.atualizadoEm,
          desatualizado: resposta.desatualizado,
        })
      } catch {
        if (!ativo) return
        setLoja((anterior) => ({
          ...anterior,
          nome: anterior.nome ?? nomeFallback,
          disponibilidade: 'indisponivel',
          desatualizado: true,
        }))
      } finally {
        if (ativo) timer = window.setTimeout(() => void carregar(), INTERVALO_ATUALIZACAO_MS)
      }
    }

    void carregar()

    return () => {
      ativo = false
      if (timer !== undefined) window.clearTimeout(timer)
    }
  }, [merchantId, nomeFallback])

  const disponibilidade = merchantId ? loja.disponibilidade : 'indisponivel'
  const online = disponibilidade === 'online'
  const carregando = disponibilidade === 'carregando'
  const statusTexto = carregando
    ? 'Consultando'
    : disponibilidade === 'online'
      ? 'Online'
      : disponibilidade === 'offline'
        ? 'Offline'
        : merchantId ? 'Indisponível' : 'Sem vínculo'
  const nome = loja.nome ?? 'Loja iFood'
  const horaAtualizacao = loja.atualizadoEm
    ? new Date(loja.atualizadoEm).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })
    : null
  const descricao = !merchantId
    ? 'Restaurante sem vínculo com o iFood.'
    : loja.desatualizado
    ? 'Não foi possível atualizar os dados agora.'
    : horaAtualizacao
      ? `Atualizado às ${horaAtualizacao}.`
      : carregando
        ? 'Buscando os dados da loja no iFood.'
        : 'Ainda não há dados atuais de disponibilidade do iFood.'

  return (
    <div
      role="status"
      aria-label={`${nome}: ${statusTexto}. ${descricao}`}
      title={`${nome} · ${descricao}`}
      className="flex min-w-0 items-center gap-2.5"
    >
      <span className="max-w-[min(38vw,12rem)] truncate text-[12px] font-medium text-texto-suave sm:max-w-48">
        {nome}
      </span>

      <span
        aria-hidden="true"
        className={`relative inline-flex h-5 w-9 shrink-0 items-center rounded-full p-0.5 transition-colors duration-200 ${
          online ? 'bg-sucesso' : 'bg-borda-forte'
        }`}
      >
        <span
          className={`size-4 rounded-full bg-superficie shadow-cartao transition-transform duration-200 ${
            online ? 'translate-x-4' : 'translate-x-0'
          } ${carregando ? 'animate-pulse' : ''}`}
        />
      </span>

      <span className={`shrink-0 text-[12px] font-semibold ${online ? 'text-sucesso' : 'text-texto-suave'}`}>
        {statusTexto}
      </span>
    </div>
  )
}

function obterNome(comerciante: unknown): string | null {
  if (!ehRegistro(comerciante)) return null
  return typeof comerciante.name === 'string' && comerciante.name.trim() ? comerciante.name : null
}

function obterDisponibilidade(status: unknown): 'online' | 'offline' | 'indisponivel' {
  if (!Array.isArray(status)) return 'indisponivel'

  const operacoesEntrega = status.filter((item) =>
    ehRegistro(item) && ['delivery', 'entrega'].includes(normalizar(item.operation ?? item.operacao)),
  )

  const statusIfood = operacoesEntrega.find((item) =>
    ehRegistro(item) && ehCanalIfood(item.salesChannel ?? item.channelDeVendas ?? item.canalDeVendas),
  )
  const statusEntrega = statusIfood ?? (operacoesEntrega.length === 1 ? operacoesEntrega[0] : null)

  if (!ehRegistro(statusEntrega) || typeof statusEntrega.available !== 'boolean')
    return 'indisponivel'

  return statusEntrega.available ? 'online' : 'offline'
}

function ehCanalIfood(valor: unknown): boolean {
  return ['ifood', 'ifoodapp', 'aplicativoifood'].includes(normalizar(valor))
}

function normalizar(valor: unknown): string {
  return typeof valor === 'string'
    ? valor.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().replace(/[\s_-]/g, '')
    : ''
}

function ehRegistro(valor: unknown): valor is Record<string, unknown> {
  return typeof valor === 'object' && valor !== null && !Array.isArray(valor)
}