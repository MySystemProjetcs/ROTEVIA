import { useEffect, useState } from 'react'

// Um relógio compartilhado por todos os cartões. Um setInterval por cartão
// faria dezenas de timers concorrentes num painel cheio.
export function useAgora(intervaloMs = 1000): number {
  const [agora, setAgora] = useState(() => Date.now())

  useEffect(() => {
    const id = setInterval(() => setAgora(Date.now()), intervaloMs)

    return () => clearInterval(id)
  }, [intervaloMs])

  return agora
}

export function formatarDuracao(ms: number): string {
  const total = Math.max(0, Math.floor(ms / 1000))
  const minutos = Math.floor(total / 60)
  const segundos = total % 60

  return `${minutos}:${segundos.toString().padStart(2, '0')}`
}

export function formatarDecorrido(ms: number): string {
  const minutos = Math.floor(Math.max(0, ms) / 60000)

  if (minutos < 60) return `${minutos} min`

  const horas = Math.floor(minutos / 60)

  return `${horas}h${(minutos % 60).toString().padStart(2, '0')}`
}

export function formatarDinheiro(valor: number): string {
  return valor.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
}

// Relógio do dia no cabeçalho (HH:MM local). Só exibição — nunca base de regra.
export function formatarHora(agora: number): string {
  return new Date(agora).toLocaleString('pt-BR', { hour: '2-digit', minute: '2-digit' })
}

// "SEXTA · 20 SET · 20:41" — topbar do Command Center.
export function formatarDataHora(agora: number): string {
  const d = new Date(agora)
  const dia = d.toLocaleString('pt-BR', { weekday: 'long' }).toUpperCase().split('-')[0].trim()
  const numDia = d.getDate()
  const mes = d.toLocaleString('pt-BR', { month: 'short' }).toUpperCase().replace('.', '')
  const hora = d.toLocaleString('pt-BR', { hour: '2-digit', minute: '2-digit' })
  return `${dia} · ${numDia} ${mes} · ${hora}`
}
