export type StatusVinculoEntregador = 'Convidado' | 'Ativo' | 'Suspenso'

export interface Entregador {
  linkId: string
  courierId: string
  nome: string
  telefone: string
  modeloDaMoto: string
  placa: string
  status: StatusVinculoEntregador
  /** Interruptor Online/Offline do motoboy. Sem ele, "Offline" mistura quem
      desligou com quem nunca conseguiu se desligar. */
  disponivel: boolean
}

export interface NovoEntregador {
  cpf: string
  nome: string
  telefone: string
  modeloDaMoto: string
  placa: string
  email: string
}

export interface EntregadorConvidado {
  courierId: string
  linkId: string
  conviteUrl: string
  enviadoPeloWhatsApp: boolean
}
