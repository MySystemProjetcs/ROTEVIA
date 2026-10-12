import { useEffect, useState } from 'react'
import { api } from '@/lib/api'

type Disponibilidade = 'online' | 'offline' | 'indisponivel' | 'sem-vinculo'

interface StatusPlataforma {
  vinculado: boolean
  autenticado: boolean
  lojaNome: string | null
  disponibilidade: Disponibilidade
  atualizadoEm: string | null
  erro: string | null
}

interface StatusIntegracoesResponse {
  ifood: StatusPlataforma
  noventaENove: StatusPlataforma
}

const INTERVALO_ATUALIZACAO_MS = 5 * 60 * 1000

const CARREGANDO: StatusPlataforma = {
  vinculado: false,
  autenticado: false,
  lojaNome: null,
  disponibilidade: 'indisponivel',
  atualizadoEm: null,
  erro: null,
}

export function EstadoIntegracoes({
  merchantId,
  nomeFallback,
}: {
  merchantId: string | null
  nomeFallback: string | null
}) {
  const [status, setStatus] = useState<StatusIntegracoesResponse | null>(null)
  const [carregando, setCarregando] = useState(true)

  useEffect(() => {
    let ativo = true
    let timer: number | undefined

    if (!merchantId) {
      setCarregando(false)
      return
    }

    async function carregar() {
      try {
        const resposta = await api.get<StatusIntegracoesResponse>(
          `/restaurantes/${merchantId}/integracoes/status`,
        )
        if (!ativo) return
        setStatus(resposta)
      } catch {
        if (!ativo) return
        // Falha na própria Api (rede, 500): as duas plataformas ficam
        // indisponíveis porque não dá para afirmar nada sobre elas.
        setStatus({
          ifood: { ...CARREGANDO, erro: 'Não foi possível consultar.' },
          noventaENove: { ...CARREGANDO, erro: 'Não foi possível consultar.' },
        })
      } finally {
        if (ativo) {
          setCarregando(false)
          timer = window.setTimeout(() => void carregar(), INTERVALO_ATUALIZACAO_MS)
        }
      }
    }

    void carregar()

    return () => {
      ativo = false
      if (timer !== undefined) window.clearTimeout(timer)
    }
  }, [merchantId])

  if (!merchantId) return null

  return (
    <div className="flex min-w-0 flex-wrap items-center gap-x-4 gap-y-1.5">
      <Plataforma
        rotulo="iFood"
        status={status?.ifood}
        carregando={carregando}
        nomeFallback={nomeFallback}
      />
      <Plataforma
        rotulo="99Food"
        status={status?.noventaENove}
        carregando={carregando}
        nomeFallback={null}
      />
    </div>
  )
}

function Plataforma({
  rotulo,
  status,
  carregando,
  nomeFallback,
}: {
  rotulo: string
  status: StatusPlataforma | undefined
  carregando: boolean
  nomeFallback: string | null
}) {
  const disponibilidade: Disponibilidade = status?.disponibilidade ?? 'indisponivel'
  const online = !carregando && disponibilidade === 'online'
  const nome = status?.lojaNome ?? nomeFallback

  const texto = carregando
    ? 'Consultando'
    : disponibilidade === 'online'
      ? 'Online'
      : disponibilidade === 'offline'
        ? 'Offline'
        : disponibilidade === 'sem-vinculo'
          ? 'Sem vínculo'
          : 'Indisponível'

  // O erro vem direto da plataforma (ex.: "errno=10101 The store authorization
  // information does not exist") — é o que o lojista precisa mandar para o
  // suporte, então aparece no title em vez de virar um texto genérico.
  const hora = status?.atualizadoEm
    ? new Date(status.atualizadoEm).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })
    : null
  const detalhe = status?.erro
    ? status.erro
    : disponibilidade === 'sem-vinculo'
      ? `Restaurante sem vínculo com o ${rotulo}.`
      : hora
        ? `Verificado às ${hora}.`
        : carregando
          ? 'Consultando a plataforma.'
          : 'Sem dados de disponibilidade.'

  return (
    <div
      role="status"
      aria-label={`${rotulo}${nome ? ` — ${nome}` : ''}: ${texto}. ${detalhe}`}
      title={`${rotulo}${nome ? ` · ${nome}` : ''} · ${detalhe}`}
      className="flex min-w-0 items-center gap-2"
    >
      <span className="shrink-0 text-[12px] font-semibold text-texto-suave">{rotulo}</span>

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

      <span
        className={`shrink-0 text-[12px] font-medium ${online ? 'text-sucesso' : 'text-texto-suave'}`}
      >
        {texto}
      </span>
    </div>
  )
}
