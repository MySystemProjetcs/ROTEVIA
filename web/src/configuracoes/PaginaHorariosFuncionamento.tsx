import { useEffect, useMemo, useRef, useState } from 'react'
import { Botao } from '@/components/Botao'
import { Cartao, CartaoCorpo } from '@/components/Cartao'
import { Dialogo } from '@/components/Dialogo'
import { Switch } from '@/components/Switch'
import { useAgora } from '@/lib/tempo'
import { cn } from '@/lib/cn'
import { DIAS_SEMANA, CANAIS } from './horarios/tipos'
import type { AgendaCanal, Canal, DiaDaAgenda, DiaSemana, Faixa, RotuloFaixa } from './horarios/tipos'
import { emHHmm, emMinutos, novaChave, rotularPorHora, useAgendaCanal } from './horarios/useAgendaCanal'

const INTERVALO_MIN_IFOOD = 30

const COPIA_SEG_SEX: DiaSemana[] = ['segunda', 'terca', 'quarta', 'quinta', 'sexta']

interface Diagnostico {
  totalErros: number
  // erro por dia (único, prioritário)
  porDia: Partial<Record<DiaSemana, string>>
}

function validar(agenda: AgendaCanal, canal: Canal): Diagnostico {
  const porDia: Diagnostico['porDia'] = {}
  let totalErros = 0
  for (const d of agenda.dias) {
    if (!d.aberto) continue
    if (d.faixas.length === 0) {
      porDia[d.dia] = 'Dia aberto precisa de ao menos 1 faixa.'
      totalErros += 1
      continue
    }
    const limites: { ini: number; fim: number }[] = []
    for (const f of d.faixas) {
      const ini = emMinutos(f.inicio)
      const fim = emMinutos(f.fim)
      if (ini === null || fim === null) {
        porDia[d.dia] = 'Horário inválido (use HH:mm).'
        totalErros += 1
        break
      }
      if (fim <= ini) {
        porDia[d.dia] = 'O fim precisa ser depois do início.'
        totalErros += 1
        break
      }
      if (canal === 'ifood' && (ini % INTERVALO_MIN_IFOOD !== 0 || fim % INTERVALO_MIN_IFOOD !== 0)) {
        porDia[d.dia] = 'iFood aceita apenas múltiplos de 30 min.'
        totalErros += 1
        break
      }
      limites.push({ ini, fim })
    }
    if (porDia[d.dia]) continue
    limites.sort((a, b) => a.ini - b.ini)
    for (let i = 1; i < limites.length; i += 1) {
      if (limites[i].ini < limites[i - 1].fim) {
        porDia[d.dia] = 'Faixas se sobrepõem.'
        totalErros += 1
        break
      }
    }
  }
  return { totalErros, porDia }
}

// "Fecha às HH:mm" ou "Abre às HH:mm" calculados a partir da agenda do canal
// ativo contra a hora de agora (local — basta pra UI; fuso real = America/Sao_Paulo).
function estadoDaLoja(agenda: AgendaCanal | null, agora: number) {
  if (!agenda) return null
  const data = new Date(agora)
  const diaJs = data.getDay() // 0=Dom, 1=Seg…
  const mapaJs: DiaSemana[] = ['domingo', 'segunda', 'terca', 'quarta', 'quinta', 'sexta', 'sabado']
  const dia = agenda.dias.find((d) => d.dia === mapaJs[diaJs])
  const horaMin = data.getHours() * 60 + data.getMinutes()
  if (dia?.aberto) {
    for (const f of dia.faixas) {
      const ini = emMinutos(f.inicio)
      const fim = emMinutos(f.fim)
      if (ini === null || fim === null) continue
      if (horaMin >= ini && horaMin < fim) {
        return { aberta: true as const, horario: f.fim }
      }
    }
  }
  // Próxima abertura (olha o resto da semana começando por hoje)
  for (let delta = 0; delta < 7; delta += 1) {
    const alvo = agenda.dias.find((d) => d.dia === mapaJs[(diaJs + delta) % 7])
    if (!alvo?.aberto) continue
    for (const f of alvo.faixas) {
      const ini = emMinutos(f.inicio)
      if (ini === null) continue
      if (delta === 0 && ini <= horaMin) continue
      return { aberta: false as const, horario: f.inicio }
    }
  }
  return { aberta: false as const, horario: null }
}

export function PaginaHorariosFuncionamento() {
  const [canalAtivo, setCanalAtivo] = useState<Canal>('ifood')
  const [canalDesejado, setCanalDesejado] = useState<Canal | null>(null)
  const estado = useAgendaCanal(canalAtivo)
  const agora = useAgora(60_000)
  const diag = useMemo(() => (estado.agenda ? validar(estado.agenda, canalAtivo) : { totalErros: 0, porDia: {} }), [estado.agenda, canalAtivo])
  const estadoLoja = useMemo(() => estadoDaLoja(estado.agenda, agora), [estado.agenda, agora])

  // Atalho ⌘S / Ctrl+S salva o canal ativo.
  useEffect(() => {
    const h = (e: KeyboardEvent) => {
      const combo = (e.metaKey || e.ctrlKey) && (e.key === 's' || e.key === 'S')
      if (!combo) return
      e.preventDefault()
      if (estado.dirty && diag.totalErros === 0 && estado.disponivel) void estado.salvar()
    }
    window.addEventListener('keydown', h)
    return () => window.removeEventListener('keydown', h)
  }, [estado, diag.totalErros])

  function tentarTrocarCanal(novo: Canal) {
    if (novo === canalAtivo) return
    if (estado.dirty) setCanalDesejado(novo)
    else setCanalAtivo(novo)
  }

  function confirmarTroca() {
    if (!canalDesejado) return
    estado.descartar()
    setCanalAtivo(canalDesejado)
    setCanalDesejado(null)
  }

  function alterarDia(dia: DiaSemana, mut: (d: DiaDaAgenda) => DiaDaAgenda) {
    estado.definir((a) => ({ ...a, dias: a.dias.map((d) => (d.dia === dia ? mut(d) : d)) }))
  }

  function alternarAberto(dia: DiaSemana, aberto: boolean) {
    alterarDia(dia, (d) => {
      if (aberto && d.faixas.length === 0) {
        return { ...d, aberto: true, faixas: [{ chave: novaChave(), inicio: '18:30', fim: '23:30', rotulo: 'jantar' }] }
      }
      return { ...d, aberto }
    })
  }

  function sugerirProximaFaixa(d: DiaDaAgenda): Faixa {
    if (d.faixas.length === 0) return { chave: novaChave(), inicio: '18:00', fim: '23:30', rotulo: 'jantar' }
    const ultima = d.faixas[d.faixas.length - 1]
    const fimUlt = emMinutos(ultima.fim)
    const inicio = fimUlt !== null ? emHHmm(fimUlt + 30) : '19:00'
    const fim = fimUlt !== null ? emHHmm(fimUlt + 30 + 180) : '23:30'
    return { chave: novaChave(), inicio, fim, rotulo: rotularPorHora(inicio) }
  }

  function adicionarFaixa(dia: DiaSemana) {
    alterarDia(dia, (d) => ({ ...d, aberto: true, faixas: [...d.faixas, sugerirProximaFaixa(d)] }))
  }

  function removerFaixa(dia: DiaSemana, chave: string) {
    alterarDia(dia, (d) => ({ ...d, faixas: d.faixas.filter((f) => f.chave !== chave) }))
  }

  function editarFaixa(dia: DiaSemana, chave: string, campos: Partial<Faixa>) {
    alterarDia(dia, (d) => ({
      ...d,
      faixas: d.faixas.map((f) => (f.chave === chave ? { ...f, ...campos, rotulo: campos.inicio ? rotularPorHora(campos.inicio) : (campos.rotulo ?? f.rotulo) } : f)),
    }))
  }

  function copiarParaSemana(dia: DiaSemana) {
    estado.definir((a) => {
      const origem = a.dias.find((d) => d.dia === dia)
      if (!origem?.aberto) return a
      return {
        ...a,
        dias: a.dias.map((d) =>
          d.dia === dia ? d : { ...d, aberto: true, faixas: origem.faixas.map((f) => ({ ...f, chave: novaChave() })) },
        ),
      }
    })
  }

  function aplicarSegASex() {
    estado.definir((a) => {
      const seg = a.dias.find((d) => d.dia === 'segunda')
      if (!seg?.aberto) return a
      return {
        ...a,
        dias: a.dias.map((d) =>
          COPIA_SEG_SEX.includes(d.dia) && d.dia !== 'segunda'
            ? { ...d, aberto: true, faixas: seg.faixas.map((f) => ({ ...f, chave: novaChave() })) }
            : d,
        ),
      }
    })
  }

  function presetJantarEmTodos() {
    estado.definir((a) => ({
      ...a,
      dias: a.dias.map((d) => ({
        ...d,
        aberto: true,
        faixas: [{ chave: novaChave(), inicio: '18:30', fim: '23:30', rotulo: 'jantar' }],
      })),
    }))
  }

  if (estado.carregando && !estado.agenda) {
    return <div className="p-6 text-texto-mudo">Carregando agenda…</div>
  }
  if (!estado.agenda) {
    return <div className="p-6 text-perigo">{estado.erro ?? 'Agenda indisponível.'}</div>
  }

  const canalLabel = CANAIS.find((c) => c.canal === canalAtivo)?.rotulo ?? canalAtivo
  const podeSalvar = estado.dirty && diag.totalErros === 0 && estado.disponivel && !estado.salvando

  return (
    <div className="flex min-h-full flex-col gap-5">
      {/* Header + chip de estado */}
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <p className="font-mono text-rotulo uppercase tracking-wide text-texto-mudo">
            Gestão · Canais de venda
          </p>
          <h1 className="mt-1 text-2xl font-bold tracking-tight text-texto">Horários de funcionamento</h1>
          <p className="mt-1 max-w-xl text-corpo text-texto-fraco">
            Cada canal tem sua própria agenda. Defina quando a loja aceita pedidos em cada um.
          </p>
        </div>
        {estadoLoja && (
          <span
            className={cn(
              'inline-flex items-center gap-2 rounded-full border px-3 py-1.5 text-corpo',
              estadoLoja.aberta
                ? 'border-sucesso/30 bg-sucesso-fundo text-sucesso'
                : 'border-marca-500/30 bg-marca-500/10 text-marca-400',
            )}
          >
            <span className={cn('size-2 rounded-full', estadoLoja.aberta ? 'bg-sucesso' : 'bg-marca-400')} />
            {estadoLoja.aberta
              ? `Loja aberta agora · fecha ${estadoLoja.horario}`
              : estadoLoja.horario
                ? `Fechada · abre ${estadoLoja.horario}`
                : 'Fechada'}
          </span>
        )}
      </header>

      {/* Abas de canal — roxo marca como cor-base, hover em roxo mais claro */}
      <nav className="flex items-center gap-1 border-b border-borda" aria-label="Canais de venda">
        {CANAIS.map((c) => (
          <button
            key={c.canal}
            type="button"
            onClick={() => tentarTrocarCanal(c.canal)}
            className={cn(
              'relative inline-flex items-center gap-2 rounded-t-controle px-4 py-3 text-corpo transition-colors',
              c.canal === canalAtivo
                ? 'bg-marca-600/10 text-marca-300 hover:bg-marca-600/20'
                : 'text-texto-mudo hover:bg-marca-600/5 hover:text-marca-300',
            )}
            aria-current={c.canal === canalAtivo}
          >
            <span className="font-medium">{c.rotulo}</span>
            {!c.ativo && (
              <span className="rounded-full border border-borda bg-superficie-alt px-2 py-0.5 text-rotulo text-texto-mudo">
                em breve
              </span>
            )}
            {c.canal === canalAtivo && (
              <span className="absolute inset-x-2 -bottom-px h-0.5 rounded-full bg-marca-600" aria-hidden />
            )}
          </button>
        ))}
      </nav>

      {/* Grid 280px / 1fr */}
      <div className="grid min-h-0 flex-1 grid-cols-1 gap-4 lg:grid-cols-[280px_1fr]">
        <aside className="flex flex-col gap-3">
          <Cartao>
            <CartaoCorpo className="space-y-2">
              <p className="font-mono text-rotulo uppercase text-texto-mudo">Agenda do {canalLabel}</p>
              <p className="text-corpo text-texto-fraco">
                {canalAtivo === 'ifood'
                  ? 'As alterações são enviadas direto para o iFood.'
                  : 'A loja própria ainda não grava horários — backend em breve.'}
              </p>
            </CartaoCorpo>
          </Cartao>

          <Cartao>
            <CartaoCorpo className="space-y-2">
              <p className="font-mono text-rotulo uppercase text-texto-mudo">Ações rápidas</p>
              <Botao variante="secundario" tamanho="pequeno" onClick={aplicarSegASex} className="w-full justify-start">
                Aplicar seg–sex a todos
              </Botao>
              <Botao variante="secundario" tamanho="pequeno" onClick={presetJantarEmTodos} className="w-full justify-start">
                18:30 – 23:30 em todos
              </Botao>
              <Botao variante="sutil" tamanho="pequeno" disabled className="w-full justify-start" title="Em breve">
                Fechar hoje por emergência
              </Botao>
            </CartaoCorpo>
          </Cartao>

          <Cartao>
            <CartaoCorpo>
              <p className="font-mono text-rotulo uppercase text-texto-mudo">Fuso horário</p>
              <p className="mt-1 text-corpo text-texto">{estado.agenda.fuso} · GMT-3</p>
            </CartaoCorpo>
          </Cartao>
        </aside>

        {/* Tabela de dias — cada célula ganha borda vertical no desktop
            via `sm:[&>*:not(:last-child)]:border-r`. Mantém o cartão limpo
            em mobile (uma coluna). */}
        <section className="flex flex-col gap-3">
          <Cartao>
            <div className="divide-y divide-borda">
              <div className="hidden grid-cols-[160px_72px_minmax(0,1fr)_44px] items-center font-mono text-rotulo uppercase text-texto-mudo sm:grid sm:[&>*:not(:last-child)]:border-r sm:[&>*:not(:last-child)]:border-borda">
                <span className="px-4 py-3">Dia</span>
                <span className="px-4 py-3">Status</span>
                <span className="px-4 py-3">Faixas de atendimento</span>
                <span className="px-4 py-3 text-right">+</span>
              </div>
              {estado.agenda.dias.map((d, idx) => (
                <LinhaDoDia
                  key={d.dia}
                  dia={d}
                  erro={diag.porDia[d.dia]}
                  destacado={d.aberto && d.faixas.length > 1}
                  primeira={idx === 0}
                  onAlternar={(v) => alternarAberto(d.dia, v)}
                  onEditarFaixa={(chave, campos) => editarFaixa(d.dia, chave, campos)}
                  onRemoverFaixa={(chave) => removerFaixa(d.dia, chave)}
                  onAdicionarFaixa={() => adicionarFaixa(d.dia)}
                  onCopiarSemana={() => copiarParaSemana(d.dia)}
                />
              ))}
            </div>
          </Cartao>

          {/* Ações do canal — vivem fora do cartão da tabela, logo abaixo. */}
          <div className="flex flex-wrap items-center justify-end gap-2">
            {(diag.totalErros > 0 || estado.erro) && (
              <span className="mr-auto text-corpo text-perigo">
                {diag.totalErros > 0 && `${diag.totalErros} erro(s) de validação`}
                {diag.totalErros > 0 && estado.erro && ' · '}
                {estado.erro}
              </span>
            )}
            <Botao variante="sutil" onClick={estado.descartar} disabled={!estado.dirty || estado.salvando}>
              Descartar
            </Botao>
            <Botao
              variante="primario"
              onClick={() => void estado.salvar()}
              disabled={!podeSalvar}
              title={!estado.disponivel ? 'Backend em breve' : undefined}
            >
              {estado.salvando ? 'Salvando…' : `Salvar horários do ${canalLabel}`}
            </Botao>
          </div>
        </section>
      </div>

      <Dialogo
        aberto={canalDesejado !== null}
        onFechar={() => setCanalDesejado(null)}
        titulo="Descartar alterações?"
      >
        <div className="space-y-4">
          <p className="text-corpo text-texto-fraco">
            Você tem alterações não salvas em <strong>{canalLabel}</strong>. Trocar de canal descarta essas alterações.
          </p>
          <div className="flex justify-end gap-2">
            <Botao variante="sutil" onClick={() => setCanalDesejado(null)}>Cancelar</Botao>
            <Botao variante="primario" onClick={confirmarTroca}>Descartar e trocar</Botao>
          </div>
        </div>
      </Dialogo>
    </div>
  )
}

// ─── Linha de um dia ────────────────────────────────────────────────────────

interface PropsLinha {
  dia: DiaDaAgenda
  erro?: string
  destacado: boolean
  primeira: boolean
  onAlternar: (v: boolean) => void
  onEditarFaixa: (chave: string, campos: Partial<Faixa>) => void
  onRemoverFaixa: (chave: string) => void
  onAdicionarFaixa: () => void
  onCopiarSemana: () => void
}

const ROTULO_DIA: Record<DiaSemana, string> =
  Object.fromEntries(DIAS_SEMANA.map((d) => [d.dia, d.rotulo])) as Record<DiaSemana, string>

function duracaoTotal(d: DiaDaAgenda): string {
  const min = d.faixas.reduce((acc, f) => {
    const ini = emMinutos(f.inicio)
    const fim = emMinutos(f.fim)
    if (ini === null || fim === null || fim <= ini) return acc
    return acc + (fim - ini)
  }, 0)
  if (min === 0) return 'fechado'
  const h = Math.floor(min / 60)
  const m = min % 60
  const faixas = d.faixas.length
  return `${h}h${m ? ` ${m}min` : ''} · ${faixas} faixa${faixas > 1 ? 's' : ''}`
}

function LinhaDoDia({
  dia, erro, destacado, primeira,
  onAlternar, onEditarFaixa, onRemoverFaixa, onAdicionarFaixa, onCopiarSemana,
}: PropsLinha) {
  const fechado = !dia.aberto
  return (
    <div
      className={cn(
        'grid grid-cols-1 items-stretch gap-3 px-4 py-3 sm:grid-cols-[160px_72px_minmax(0,1fr)_44px] sm:gap-0 sm:px-0 sm:py-0',
        'sm:[&>*:not(:last-child)]:border-r sm:[&>*:not(:last-child)]:border-borda',
        destacado && 'border-l-2 border-marca-600',
        fechado && 'opacity-60',
      )}
    >
      <div className="min-w-0 sm:px-4 sm:py-3">
        <p className="text-corpo font-medium text-texto">{ROTULO_DIA[dia.dia]}</p>
        <p className="font-mono text-rotulo uppercase text-texto-mudo">{duracaoTotal(dia)}</p>
      </div>
      {/* Switch sem rótulo visível — nome acessível mantido via sr-only. */}
      <div className="flex items-center sm:px-4 sm:py-3 [&_label>span:last-child]:sr-only">
        <Switch marcado={dia.aberto} onMudar={onAlternar} rotulo={`Abrir ${ROTULO_DIA[dia.dia]}`} />
      </div>
      <div className="flex min-w-0 flex-col gap-2 sm:px-4 sm:py-3">
        {fechado ? (
          <span className="text-corpo italic text-texto-mudo">Loja fechada neste dia</span>
        ) : (
          dia.faixas.map((f) => (
            <FaixaEditavel
              key={f.chave}
              faixa={f}
              podeRemover={dia.faixas.length > 1}
              onEditar={(campos) => onEditarFaixa(f.chave, campos)}
              onRemover={() => onRemoverFaixa(f.chave)}
            />
          ))
        )}
        {!fechado && primeira && dia.faixas.length > 0 && (
          <button
            type="button"
            onClick={onCopiarSemana}
            className="self-start text-rotulo uppercase tracking-wide text-texto-mudo underline underline-offset-4 hover:text-marca-300"
          >
            Copiar para toda semana
          </button>
        )}
        {erro && <span className="text-rotulo text-perigo">{erro}</span>}
      </div>
      <div className="flex justify-start sm:items-start sm:justify-end sm:px-4 sm:py-3">
        <button
          type="button"
          onClick={onAdicionarFaixa}
          disabled={fechado}
          aria-label={`Adicionar faixa em ${ROTULO_DIA[dia.dia]}`}
          className="rounded-controle border border-borda bg-superficie-alt px-3 py-1.5 text-corpo text-texto-fraco hover:border-marca-600 hover:text-texto disabled:cursor-not-allowed disabled:opacity-40"
        >
          +
        </button>
      </div>
    </div>
  )
}

function FaixaEditavel({
  faixa, podeRemover, onEditar, onRemover,
}: {
  faixa: Faixa
  podeRemover: boolean
  onEditar: (campos: Partial<Faixa>) => void
  onRemover: () => void
}) {
  // `[color-scheme:dark]` força o navegador a usar o indicador escuro,
  // deixando o ícone de relógio em branco no fundo da superfície.
  const classeHora =
    'h-9 w-28 rounded-controle border border-borda-forte bg-superficie px-2 text-center font-mono text-corpo tabular-nums text-texto outline-offset-2 [color-scheme:dark] focus-visible:outline-2 focus-visible:outline-marca-600'
  return (
    <div className="flex flex-wrap items-center gap-2">
      <input
        type="time"
        value={faixa.inicio}
        step={60}
        onChange={(e) => onEditar({ inicio: e.target.value })}
        className={classeHora}
        aria-label="Início"
      />
      <span className="text-corpo text-texto-mudo">até</span>
      <input
        type="time"
        value={faixa.fim}
        step={60}
        onChange={(e) => onEditar({ fim: e.target.value })}
        className={classeHora}
        aria-label="Fim"
      />
      {faixa.rotulo && <ChipRotulo rotulo={faixa.rotulo} />}
      {podeRemover && (
        <button
          type="button"
          onClick={onRemover}
          aria-label="Remover faixa"
          className="ml-auto rounded-controle border border-borda px-2 py-1 text-corpo text-texto-mudo hover:border-perigo hover:text-perigo"
        >
          ×
        </button>
      )}
    </div>
  )
}

function ChipRotulo({ rotulo }: { rotulo: NonNullable<RotuloFaixa> }) {
  const texto = rotulo === 'almoco' ? 'ALMOÇO' : 'JANTAR'
  const classe =
    rotulo === 'almoco'
      ? 'bg-sucesso/15 text-sucesso'
      : 'bg-marca-600/15 text-marca-600'
  return (
    <span className={cn('rounded-full px-2 py-0.5 font-mono text-rotulo', classe)}>{texto}</span>
  )
}
