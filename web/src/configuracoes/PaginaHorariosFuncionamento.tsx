import { useEffect, useState } from 'react'
import { useSessao } from '@/auth/SessaoProvider'
import { Botao } from '@/components/Botao'
import { Switch } from '@/components/Switch'
import { ErroDaApi, api } from '@/lib/api'

const DIAS = [
  { chave: 'MONDAY', rotulo: 'Segunda-feira' },
  { chave: 'TUESDAY', rotulo: 'Terça-feira' },
  { chave: 'WEDNESDAY', rotulo: 'Quarta-feira' },
  { chave: 'THURSDAY', rotulo: 'Quinta-feira' },
  { chave: 'FRIDAY', rotulo: 'Sexta-feira' },
  { chave: 'SATURDAY', rotulo: 'Sábado' },
  { chave: 'SUNDAY', rotulo: 'Domingo' },
] as const

interface TurnoEditavel {
  chave: string
  inicio: string
  fim: string
}

interface DiaEditavel {
  dia: typeof DIAS[number]['chave']
  turnos: TurnoEditavel[]
}

interface TurnoDaApi {
  id?: string
  dayOfWeek?: string
  start?: string
  duration?: number
}

interface AgendaDaApi {
  shifts?: TurnoDaApi[]
}

const CLASSE_HORA =
  'h-10 w-[6.5rem] rounded-controle border border-borda-forte bg-superficie px-2 text-center text-corpo tabular-nums text-texto outline-offset-2 focus-visible:outline-2 focus-visible:outline-marca-600 disabled:opacity-60'

function novoTurno(inicio = '10:00', fim = '18:00'): TurnoEditavel {
  return { chave: globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random()}`, inicio, fim }
}

function minutos(hora: string): number | null {
  const encontrado = /^([01]\d|2[0-3]):([0-5]\d)$/.exec(hora)
  return encontrado ? Number(encontrado[1]) * 60 + Number(encontrado[2]) : null
}

function validarTurnosDoDia(rotulo: string, turnos: TurnoEditavel[]): string | null {
  const intervalos: { inicio: number; fimEfetivo: number }[] = []

  for (const turno of turnos) {
    const inicio = minutos(turno.inicio)
    const fim = minutos(turno.fim)
    if (inicio === null || fim === null) continue

    if (inicio % 30 !== 0 || fim % 30 !== 0) {
      return `O iFood aceita horários em intervalos de 30 min (ex.: 18:00, 18:30). Ajuste ${rotulo}.`
    }

    intervalos.push({ inicio, fimEfetivo: fim > inicio ? fim : fim + 1440 })
  }

  intervalos.sort((a, b) => a.inicio - b.inicio)
  for (let indice = 1; indice < intervalos.length; indice += 1) {
    if (intervalos[indice].inicio < intervalos[indice - 1].fimEfetivo) {
      return `Turnos sobrepostos em ${rotulo}.`
    }
  }

  return null
}

function viraODia(inicio: string, fim: string): boolean {
  const inicioMinutos = minutos(inicio)
  const fimMinutos = minutos(fim)
  return inicioMinutos !== null && fimMinutos !== null && fimMinutos <= inicioMinutos
}

function paraHora(minutoDoDia: number): string {
  const normalizado = ((minutoDoDia % 1440) + 1440) % 1440
  return `${Math.floor(normalizado / 60).toString().padStart(2, '0')}:${(normalizado % 60).toString().padStart(2, '0')}`
}

function extrairTurnos(valor: unknown): DiaEditavel[] {
  const dias = new Map<string, TurnoEditavel[]>()
  const agendas = Array.isArray(valor)
    ? valor as AgendaDaApi[]
    : typeof valor === 'object' && valor !== null
      ? [valor as AgendaDaApi]
      : []

  for (const agenda of agendas) {
    for (const turno of agenda?.shifts ?? []) {
      if (!turno.dayOfWeek || !turno.start || !Number.isFinite(turno.duration)) continue
      const dia = turno.dayOfWeek.toUpperCase()
      if (!DIAS.some((item) => item.chave === dia)) continue

      const inicio = turno.start.slice(0, 5)
      const inicioMinutos = minutos(inicio)
      if (inicioMinutos === null || !turno.duration || turno.duration <= 0) continue

      const turnos = dias.get(dia) ?? []
      turnos.push(novoTurno(inicio, paraHora(inicioMinutos + turno.duration)))
      dias.set(dia, turnos)
    }
  }

  return DIAS.map(({ chave }) => ({ dia: chave, turnos: dias.get(chave) ?? [] }))
}

export function PaginaHorariosFuncionamento() {
  const { usuario } = useSessao()
  const semVinculo = !usuario?.merchantId
  const [dias, setDias] = useState<DiaEditavel[]>(() => DIAS.map(({ chave }) => ({ dia: chave, turnos: [] })))
  const [carregando, setCarregando] = useState(() => !semVinculo)
  const [falhaCarregamento, setFalhaCarregamento] = useState(false)
  const [tentativaCarga, setTentativaCarga] = useState(0)
  const [salvando, setSalvando] = useState(false)
  const [alterado, setAlterado] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  const [mensagem, setMensagem] = useState<string | null>(null)

  useEffect(() => {
    let ativo = true

    async function carregar() {
      if (!usuario?.merchantId) return

      try {
        const agenda = await api.get<unknown>('/restaurantes/ifood/horario-funcionamento')
        if (ativo) setDias(extrairTurnos(agenda))
      } catch (falha) {
        if (!ativo) return
        if (falha instanceof ErroDaApi && falha.status === 404) {
          setDias(DIAS.map(({ chave }) => ({ dia: chave, turnos: [] })))
        } else {
          setFalhaCarregamento(true)
          setErro(falha instanceof Error ? falha.message : 'Não foi possível carregar os horários do iFood.')
        }
      } finally {
        if (ativo) setCarregando(false)
      }
    }

    void carregar()
    return () => { ativo = false }
  }, [usuario?.merchantId, tentativaCarga])

  function atualizarDia(dia: DiaEditavel['dia'], turnos: TurnoEditavel[]) {
    setDias((atuais) => atuais.map((item) => item.dia === dia ? { ...item, turnos } : item))
    setAlterado(true)
    setErro(null)
    setMensagem(null)
  }

  function atualizarTurno(dia: DiaEditavel['dia'], chave: string, campo: 'inicio' | 'fim', valor: string) {
    const atual = dias.find((item) => item.dia === dia)
    if (!atual) return
    atualizarDia(dia, atual.turnos.map((turno) => turno.chave === chave ? { ...turno, [campo]: valor } : turno))
  }

  async function salvar() {
    for (const dia of dias) {
      const rotulo = DIAS.find((item) => item.chave === dia.dia)?.rotulo ?? dia.dia
      const problema = validarTurnosDoDia(rotulo, dia.turnos)
      if (problema) {
        setErro(problema)
        return
      }
    }

    const shifts: { dayOfWeek: string; start: string; duration: number }[] = []

    for (const dia of dias) {
      for (const turno of dia.turnos) {
        const inicio = minutos(turno.inicio)
        const fim = minutos(turno.fim)
        if (inicio === null || fim === null) {
          setErro(`Confira os horários de ${DIAS.find((item) => item.chave === dia.dia)?.rotulo}.`)
          return
        }

        // Mesmo horário de início e fim viraria uma jornada de 24h pelo
        // fallback do `|| 1440`, salvando a loja aberta o dia inteiro sem o
        // usuário pedir. Força a correção antes de bater no iFood.
        if (inicio === fim) {
          setErro(`O horário de ${DIAS.find((item) => item.chave === dia.dia)?.rotulo} precisa de início diferente do fim.`)
          return
        }

        shifts.push({
          dayOfWeek: dia.dia,
          start: `${turno.inicio}:00`,
          duration: (fim - inicio + 1440) % 1440,
        })
      }
    }

    setSalvando(true)
    setErro(null)
    setMensagem(null)
    try {
      const resposta = await api.put<unknown>('/restaurantes/ifood/horario-funcionamento', { shifts })
      setDias(extrairTurnos(resposta))
      setAlterado(false)
      setMensagem('Horários atualizados no iFood.')
    } catch (falha) {
      setErro(
        falha instanceof ErroDaApi && falha.status === 404
          ? 'A API ativa não reconhece a rota de horários. Pare o servidor antigo e reinicie o ambiente com ./dev.sh.'
          : falha instanceof Error
            ? falha.message
            : 'Não foi possível salvar os horários.',
      )
    } finally {
      setSalvando(false)
    }
  }

  return (
    <section className="mx-auto flex w-full max-w-5xl flex-col gap-6 pb-8">
      <header className="flex flex-wrap items-end justify-between gap-4 border-b border-borda pb-5">
        <div className="min-w-0">
          <h1 className="text-[26px] font-bold leading-tight text-texto">Horário de funcionamento</h1>
          <p className="mt-1.5 text-apoio text-texto-suave">
            Configure quando a loja recebe pedidos pelo iFood.
          </p>
        </div>
        <Botao onClick={() => void salvar()} carregando={salvando} disabled={carregando || falhaCarregamento || semVinculo || !alterado}>
          Salvar horários
        </Botao>
      </header>

      {carregando ? (
        <div className="divide-y divide-borda" aria-label="Carregando horários">
          {DIAS.map(({ chave }) => (
            <div key={chave} className="grid animate-pulse grid-cols-1 gap-3 py-5 sm:grid-cols-[minmax(9rem,1fr)_9rem_minmax(18rem,auto)] sm:items-center">
              <span className="h-4 w-28 rounded bg-superficie-afundada" />
              <span className="h-6 w-24 rounded bg-superficie-afundada" />
              <span className="h-10 w-56 rounded-controle bg-superficie-afundada sm:justify-self-end" />
            </div>
          ))}
        </div>
      ) : (
        <div className="divide-y divide-borda border-y border-borda">
          {DIAS.map(({ chave, rotulo }) => {
            const dia = dias.find((item) => item.dia === chave) ?? { dia: chave, turnos: [] }
            const aberto = dia.turnos.length > 0

            return (
              <fieldset key={chave} className="grid grid-cols-1 gap-3 py-4 sm:grid-cols-[minmax(9rem,1fr)_9rem_minmax(18rem,auto)] sm:items-center sm:py-5">
                <legend className="sr-only">{rotulo}</legend>
                <span className="text-[15px] font-semibold text-texto">{rotulo}</span>

                <Switch
                  marcado={aberto}
                  rotulo={aberto ? 'Aberto' : 'Fechado'}
                  onMudar={(marcado) => atualizarDia(chave, marcado ? [novoTurno()] : [])}
                  classeLigado="bg-sucesso"
                  disabled={salvando || falhaCarregamento || semVinculo}
                />

                <div className="flex min-w-0 flex-wrap items-center gap-2 sm:justify-end">
                  {aberto ? dia.turnos.map((turno, indice) => (
                    <div key={turno.chave} className="flex items-center gap-2">
                      {indice > 0 && <span className="sr-only">Outro intervalo</span>}
                      <label className="sr-only" htmlFor={`${chave}-${turno.chave}-inicio`}>Início, {rotulo}</label>
                      <input
                        id={`${chave}-${turno.chave}-inicio`}
                        type="time"
                        value={turno.inicio}
                        disabled={salvando || falhaCarregamento || semVinculo}
                        onChange={(evento) => atualizarTurno(chave, turno.chave, 'inicio', evento.target.value)}
                        className={CLASSE_HORA}
                      />
                      <span aria-hidden="true" className="text-texto-fraco">–</span>
                      <label className="sr-only" htmlFor={`${chave}-${turno.chave}-fim`}>Fim, {rotulo}</label>
                      <input
                        id={`${chave}-${turno.chave}-fim`}
                        type="time"
                        value={turno.fim}
                        disabled={salvando || falhaCarregamento || semVinculo}
                        onChange={(evento) => atualizarTurno(chave, turno.chave, 'fim', evento.target.value)}
                        className={CLASSE_HORA}
                      />
                      {viraODia(turno.inicio, turno.fim) && (
                        <span className="text-rotulo text-texto-fraco">vira o dia</span>
                      )}
                      {dia.turnos.length > 1 && (
                        <button
                          type="button"
                          aria-label={`Remover intervalo ${indice + 1} de ${rotulo}`}
                          title="Remover intervalo"
                          disabled={salvando || falhaCarregamento || semVinculo}
                          onClick={() => atualizarDia(chave, dia.turnos.filter((item) => item.chave !== turno.chave))}
                          className="inline-flex size-8 items-center justify-center rounded-controle text-texto-fraco outline-offset-2 hover:bg-superficie-alt hover:text-perigo focus-visible:outline-2 focus-visible:outline-marca-600 disabled:opacity-50"
                        >
                          <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
                            <path d="M5 12h14" />
                          </svg>
                        </button>
                      )}
                    </div>
                  )) : (
                    <span className="text-apoio text-texto-mudo">Loja fechada</span>
                  )}

                  {aberto && (
                    <button
                      type="button"
                      aria-label={`Adicionar intervalo em ${rotulo}`}
                      title="Adicionar intervalo"
                      disabled={salvando || falhaCarregamento || semVinculo}
                      onClick={() => atualizarDia(chave, [...dia.turnos, novoTurno('18:00', '22:00')])}
                      className="inline-flex size-8 items-center justify-center rounded-controle text-texto-fraco outline-offset-2 hover:bg-superficie-alt hover:text-texto focus-visible:outline-2 focus-visible:outline-marca-600 disabled:opacity-50"
                    >
                      <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
                        <path d="M12 5v14M5 12h14" />
                      </svg>
                    </button>
                  )}
                </div>
              </fieldset>
            )
          })}
        </div>
      )}

      <div aria-live="polite" className="min-h-6">
        {(erro || semVinculo) && (
          <div className="flex flex-wrap items-center gap-3">
            <p className="text-apoio font-medium text-perigo">
              {semVinculo ? 'Esta sessão não está vinculada a um restaurante.' : erro}
            </p>
            {falhaCarregamento && !semVinculo && (
              <Botao variante="sutil" tamanho="pequeno" onClick={() => {
                setCarregando(true)
                setErro(null)
                setFalhaCarregamento(false)
                setTentativaCarga((tentativa) => tentativa + 1)
              }}>
                Tentar novamente
              </Botao>
            )}
          </div>
        )}
        {mensagem && <p className="text-apoio font-medium text-sucesso">{mensagem}</p>}
      </div>
    </section>
  )
}