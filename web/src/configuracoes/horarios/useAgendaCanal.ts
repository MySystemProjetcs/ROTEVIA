import { useCallback, useEffect, useState } from 'react'
import { ErroDaApi, api } from '@/lib/api'
import type { AgendaCanal, Canal, DiaSemana, Faixa } from './tipos'
import { DIAS_SEMANA } from './tipos'

// Mapeamento iFood ⇆ domínio local. O iFood modela cada janela como um
// `shift` { DayOfWeek, Start (HH:mm), Duration (minutos) }; a UI opera em
// faixas { inicio HH:mm, fim HH:mm } porque é como o restaurante pensa.

const DIA_PARA_IFOOD: Record<DiaSemana, string> = {
  segunda: 'MONDAY',
  terca:   'TUESDAY',
  quarta:  'WEDNESDAY',
  quinta:  'THURSDAY',
  sexta:   'FRIDAY',
  sabado:  'SATURDAY',
  domingo: 'SUNDAY',
}
const IFOOD_PARA_DIA = Object.fromEntries(
  Object.entries(DIA_PARA_IFOOD).map(([k, v]) => [v, k as DiaSemana]),
) as Record<string, DiaSemana>

interface ShiftIFood {
  id?: string
  dayOfWeek: string
  start: string      // 'HH:mm:ss' ou 'HH:mm'
  duration: number   // minutos
}

function emMinutos(hora: string): number | null {
  const m = /^([01]\d|2[0-3]):([0-5]\d)(?::[0-5]\d)?$/.exec(hora)
  return m ? Number(m[1]) * 60 + Number(m[2]) : null
}

function emHHmm(totalMinutos: number): string {
  const base = ((totalMinutos % 1440) + 1440) % 1440
  const h = Math.floor(base / 60).toString().padStart(2, '0')
  const m = (base % 60).toString().padStart(2, '0')
  return `${h}:${m}`
}

function novaChave(): string {
  return globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random()}`
}

// Rótulo automático só pela hora de início. Não vai pro backend; é dica
// visual pra o restaurante distinguir o turno de almoço do de jantar.
function rotularPorHora(inicio: string): Faixa['rotulo'] {
  const h = emMinutos(inicio) ?? -1
  if (h >= 10 * 60 + 30 && h <= 13 * 60) return 'almoco'
  if (h >= 17 * 60 && h <= 19 * 60 + 30) return 'jantar'
  return null
}

function agendaVazia(canal: Canal): AgendaCanal {
  return {
    canal,
    fuso: 'America/Sao_Paulo',
    dias: DIAS_SEMANA.map(({ dia }) => ({ dia, aberto: false, faixas: [] })),
  }
}

function shiftsParaAgenda(shifts: ShiftIFood[]): AgendaCanal {
  const base = agendaVazia('ifood')
  const porDia = new Map<DiaSemana, Faixa[]>()
  for (const s of shifts) {
    const dia = IFOOD_PARA_DIA[s.dayOfWeek?.toUpperCase?.() ?? '']
    if (!dia) continue
    const inicioMin = emMinutos(s.start)
    if (inicioMin === null) continue
    const inicio = emHHmm(inicioMin)
    const fim = emHHmm(inicioMin + s.duration)
    const faixa: Faixa = { chave: novaChave(), inicio, fim, rotulo: rotularPorHora(inicio) }
    const lista = porDia.get(dia) ?? []
    lista.push(faixa)
    porDia.set(dia, lista)
  }
  base.dias = base.dias.map((d) => {
    const faixas = (porDia.get(d.dia) ?? []).sort(
      (a, b) => (emMinutos(a.inicio) ?? 0) - (emMinutos(b.inicio) ?? 0),
    )
    return { ...d, aberto: faixas.length > 0, faixas }
  })
  return base
}

function agendaParaShifts(agenda: AgendaCanal): ShiftIFood[] {
  const shifts: ShiftIFood[] = []
  for (const d of agenda.dias) {
    if (!d.aberto) continue
    for (const f of d.faixas) {
      const ini = emMinutos(f.inicio)
      const fim = emMinutos(f.fim)
      if (ini === null || fim === null) continue
      const dur = fim > ini ? fim - ini : (1440 - ini) + fim
      // Backend valida `Start` como `HH:mm:ss` estrito; o input `type="time"`
      // devolve `HH:mm`, então garantimos o sufixo de segundos aqui.
      shifts.push({ dayOfWeek: DIA_PARA_IFOOD[d.dia], start: `${f.inicio}:00`, duration: dur })
    }
  }
  return shifts
}

export interface EstadoAgenda {
  agenda: AgendaCanal | null
  carregando: boolean
  salvando: boolean
  erro: string | null
  dirty: boolean
  disponivel: boolean
  // Troca a agenda local inteira (preserva o canal).
  definir: (atualizador: (a: AgendaCanal) => AgendaCanal) => void
  salvar: () => Promise<void>
  descartar: () => void
}

export function useAgendaCanal(canal: Canal): EstadoAgenda {
  const [agenda, setAgenda] = useState<AgendaCanal | null>(null)
  const [original, setOriginal] = useState<string>('')
  const [carregando, setCarregando] = useState<boolean>(true)
  const [salvando, setSalvando] = useState<boolean>(false)
  const [erro, setErro] = useState<string | null>(null)

  const disponivel = canal === 'ifood' // Loja própria ainda sem backend.

  const carregar = useCallback(async () => {
    setCarregando(true)
    setErro(null)
    try {
      if (canal === 'ifood') {
        try {
          const resposta = await api.get<{ shifts: ShiftIFood[] } | ShiftIFood[]>(
            '/restaurantes/ifood/horario-funcionamento',
          )
          const lista = Array.isArray(resposta) ? resposta : (resposta?.shifts ?? [])
          const nova = shiftsParaAgenda(lista)
          setAgenda(nova)
          setOriginal(JSON.stringify(nova))
        } catch (e) {
          // 404 ⇒ loja sem agenda ainda; segue com template vazio.
          if (e instanceof ErroDaApi && e.status === 404) {
            const nova = agendaVazia(canal)
            setAgenda(nova)
            setOriginal(JSON.stringify(nova))
          } else throw e
        }
      } else {
        const nova = agendaVazia(canal)
        setAgenda(nova)
        setOriginal(JSON.stringify(nova))
      }
    } catch (e) {
      setErro(e instanceof ErroDaApi ? e.message : 'Falha ao carregar a agenda.')
    } finally {
      setCarregando(false)
    }
  }, [canal])

  useEffect(() => { void carregar() }, [carregar])

  const definir = useCallback((atualizador: (a: AgendaCanal) => AgendaCanal) => {
    setAgenda((atual) => (atual ? atualizador(atual) : atual))
  }, [])

  const salvar = useCallback(async () => {
    if (!agenda || !disponivel) return
    setSalvando(true)
    setErro(null)
    try {
      if (canal === 'ifood') {
        const shifts = agendaParaShifts(agenda)
        const resposta = await api.put<{ shifts: ShiftIFood[] } | ShiftIFood[]>(
          '/restaurantes/ifood/horario-funcionamento',
          { shifts },
        )
        const lista = Array.isArray(resposta) ? resposta : (resposta?.shifts ?? [])
        const nova = shiftsParaAgenda(lista)
        setAgenda(nova)
        setOriginal(JSON.stringify(nova))
      }
    } catch (e) {
      setErro(e instanceof ErroDaApi ? e.message : 'Falha ao salvar a agenda.')
      throw e
    } finally {
      setSalvando(false)
    }
  }, [agenda, canal, disponivel])

  const descartar = useCallback(() => {
    if (!original) return
    setAgenda(JSON.parse(original))
    setErro(null)
  }, [original])

  const dirty = agenda !== null && JSON.stringify(agenda) !== original

  return { agenda, carregando, salvando, erro, dirty, disponivel, definir, salvar, descartar }
}

export { emMinutos, emHHmm, novaChave, rotularPorHora }
