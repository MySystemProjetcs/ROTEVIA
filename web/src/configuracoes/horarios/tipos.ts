// Tipos de domínio da agenda de atendimento por canal de venda.
// Mantidos aqui porque são compartilhados entre a página, o hook e as
// validações; backend fala em outros formatos (iFood: shifts) e a conversão
// vive no `useAgendaCanal`.

export type Canal = 'ifood' | 'propria'

export type DiaSemana =
  | 'segunda'
  | 'terca'
  | 'quarta'
  | 'quinta'
  | 'sexta'
  | 'sabado'
  | 'domingo'

export type RotuloFaixa = 'almoco' | 'jantar' | null

export interface Faixa {
  // Chave estável só pro React — nunca sai pro backend.
  chave: string
  // Formato HH:mm (24h).
  inicio: string
  fim: string
  rotulo: RotuloFaixa
}

export interface DiaDaAgenda {
  dia: DiaSemana
  aberto: boolean
  faixas: Faixa[]
}

export interface AgendaCanal {
  canal: Canal
  fuso: string
  dias: DiaDaAgenda[]
}

export const DIAS_SEMANA: readonly { dia: DiaSemana; rotulo: string }[] = [
  { dia: 'segunda', rotulo: 'Segunda' },
  { dia: 'terca',   rotulo: 'Terça'   },
  { dia: 'quarta',  rotulo: 'Quarta'  },
  { dia: 'quinta',  rotulo: 'Quinta'  },
  { dia: 'sexta',   rotulo: 'Sexta'   },
  { dia: 'sabado',  rotulo: 'Sábado'  },
  { dia: 'domingo', rotulo: 'Domingo' },
]

export const CANAIS: readonly { canal: Canal; rotulo: string; ativo: boolean }[] = [
  { canal: 'ifood',   rotulo: 'iFood',       ativo: true  },
  { canal: 'propria', rotulo: 'Loja própria', ativo: false },
  // 99Food fica fora até o desenvolvimento voltar — ver memória do projeto.
]
