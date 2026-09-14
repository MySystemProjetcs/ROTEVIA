import { readdir } from 'node:fs/promises'
import QRCode from 'qrcode'
import pkg from 'whatsapp-web.js'

const { Client, LocalAuth } = pkg
import { config } from './config.js'
import { notificarStatusDaSessao } from './api.js'

// Vocabulário de status em inglês de propósito: é o contrato com a API, que
// mapeia pra StatusConexaoWhatsApp (enum em português) do domínio .NET.
// merchantId -> { cliente, status, qrDataUrl, telefone, iniciando }
const sessoes = new Map()

// merchantId -> Promise da tentativa de conexão em andamento. Sem isto, a
// restauração automática do boot e um pedido de "Conectar" simultâneo (ou
// dois cliques seguidos) disparam dois `new Client()` pro mesmo userDataDir
// ao mesmo tempo — o Chromium só aceita um dono do perfil por vez.
const conexoesEmAndamento = new Map()

export function paraIdDeChat(telefone) {
  const digitos = String(telefone).replace(/\D/g, '')
  return `${digitos}@c.us`
}

export function paraTelefone(idDeChat) {
  return String(idDeChat).replace(/@.*$/, '')
}

function definirEstado(merchantId, parcial) {
  const atual = sessoes.get(merchantId) ?? {}
  sessoes.set(merchantId, { ...atual, ...parcial })
}

export function obterStatus(merchantId) {
  const s = sessoes.get(merchantId)
  if (!s) return { status: 'DISCONNECTED', qrDataUrl: null, telefone: null }
  return { status: s.status ?? 'DISCONNECTED', qrDataUrl: s.qrDataUrl ?? null, telefone: s.telefone ?? null }
}

// Ponto de entrada público: garante no máximo uma tentativa de conexão por
// merchant rodando por vez, sem isso chamadas concorrentes (boot restaurando
// + clique em "Conectar", por exemplo) disputam o mesmo userDataDir.
export function conectarSessao(merchantId) {
  const emAndamento = conexoesEmAndamento.get(merchantId)
  if (emAndamento) return emAndamento

  const promessa = iniciarConexao(merchantId).finally(() => conexoesEmAndamento.delete(merchantId))
  conexoesEmAndamento.set(merchantId, promessa)
  return promessa
}

async function iniciarConexao(merchantId) {
  const existente = sessoes.get(merchantId)
  if (existente?.cliente && ['CONNECTED', 'QR_PENDING', 'SCANNING'].includes(existente.status)) {
    return obterStatus(merchantId)
  }

  const cliente = new Client({
    authStrategy: new LocalAuth({ clientId: String(merchantId), dataPath: config.pastaSessoes }),
    puppeteer: {
      headless: true,
      args: ['--no-sandbox', '--disable-setuid-sandbox', '--disable-dev-shm-usage'],
    },
  })

  definirEstado(merchantId, { cliente, status: 'DISCONNECTED', qrDataUrl: null, telefone: null, iniciando: true })

  cliente.on('qr', async (qr) => {
    try {
      const qrDataUrl = await QRCode.toDataURL(qr)
      definirEstado(merchantId, { qrDataUrl, status: 'QR_PENDING' })
      await notificarStatusDaSessao({ merchantId, status: 'QR_PENDING', qrDataUrl })
    } catch (erro) {
      console.error(`[zap:${merchantId}] falha ao gerar QR:`, erro.message)
    }
  })

  cliente.on('authenticated', async () => {
    definirEstado(merchantId, { status: 'SCANNING', qrDataUrl: null })
    await notificarStatusDaSessao({ merchantId, status: 'SCANNING' })
  })

  cliente.on('ready', async () => {
    const telefone = cliente.info?.wid ? paraTelefone(cliente.info.wid._serialized) : null
    definirEstado(merchantId, { status: 'CONNECTED', qrDataUrl: null, telefone, iniciando: false })
    await notificarStatusDaSessao({ merchantId, status: 'CONNECTED', telefone })
    console.log(`[zap:${merchantId}] conectado (${telefone ?? '?'})`)
  })

  cliente.on('auth_failure', async (mensagem) => {
    definirEstado(merchantId, { status: 'ERROR', iniciando: false })
    await notificarStatusDaSessao({ merchantId, status: 'ERROR', erro: String(mensagem) })
  })

  cliente.on('disconnected', async (motivo) => {
    console.log(`[zap:${merchantId}] desconectado:`, motivo)
    try {
      cliente.destroy()
    } catch {
      // ignora
    }
    sessoes.delete(merchantId)
    await notificarStatusDaSessao({ merchantId, status: 'DISCONNECTED' })
  })

  // Sem listener de 'message' de propósito: ao conectar, o whatsapp-web.js
  // reemite mensagens recentes dos chats como parte da própria sincronização
  // — isso capturaria conversa pessoal de quem conectou, não só resposta de
  // motoboy. Nada neste passo consome mensagem recebida (é convite por
  // enquanto, só saída); reativar isso pede um filtro antes, não só religar.

  try {
    await cliente.initialize()
  } catch (erro) {
    definirEstado(merchantId, { status: 'ERROR', iniciando: false })
    console.error(`[zap:${merchantId}] falha ao inicializar:`, erro.message)
  }

  return obterStatus(merchantId)
}

export async function desconectarSessao(merchantId) {
  const s = sessoes.get(merchantId)
  if (!s?.cliente) {
    sessoes.delete(merchantId)
    return { status: 'DISCONNECTED', qrDataUrl: null, telefone: null }
  }

  try {
    await s.cliente.logout()
  } catch {
    // ignora
  }
  try {
    await s.cliente.destroy()
  } catch {
    // ignora
  }

  sessoes.delete(merchantId)
  await notificarStatusDaSessao({ merchantId, status: 'DISCONNECTED' })
  return { status: 'DISCONNECTED', qrDataUrl: null, telefone: null }
}

export async function enviarMensagem(merchantId, para, texto) {
  const s = sessoes.get(merchantId)
  if (!s?.cliente || s.status !== 'CONNECTED') {
    const erro = new Error('Sessão do WhatsApp não está conectada.')
    erro.codigo = 'NAO_CONECTADO'
    throw erro
  }

  const digitos = String(para).replace(/\D/g, '')
  if (digitos.length < 10 || digitos.length > 15) {
    const erro = new Error(`Número de destino inválido: "${para}". Use DDI+DDD+número.`)
    erro.codigo = 'NUMERO_INVALIDO'
    throw erro
  }

  const idDeChat = paraIdDeChat(digitos)
  try {
    const enviada = await s.cliente.sendMessage(idDeChat, String(texto))
    const externalId = enviada?.id?._serialized ?? null
    return { externalId }
  } catch (erro) {
    const mensagem = String(erro?.message ?? erro)
    if (/no lid|not on whatsapp|invalid|not-authorized|no such/i.test(mensagem)) {
      const amigavel = new Error(
        `Não foi possível entregar ao número ${digitos}: contato inexistente no WhatsApp ou sessão perdeu o pareamento. Reconecte a sessão e confira o número.`,
      )
      amigavel.codigo = 'NAO_ENTREGAVEL'
      throw amigavel
    }
    throw erro
  }
}

// Fecha o Chromium de cada sessão sem logout — mata a sessão o processo
// de fato inteiro, mas sem isto o Node morre e deixa o Chrome órfão segurando
// o lock do userDataDir, travando a restauração automática na próxima subida.
export async function encerrarTodasAsSessoes() {
  const pendentes = [...sessoes.values()]
    .filter((s) => s.cliente)
    .map((s) => s.cliente.destroy().catch(() => {}))

  await Promise.all(pendentes)
}

// LocalAuth grava a sessão autenticada em disco (pastaSessoes/session-{id}) —
// sobrevive a reiniciar o processo. O que não sobrevive é o Map em memória:
// sem isto, depois de um restart a sessão fica autenticada no disco mas
// ninguém chama conectarSessao de novo até alguém abrir a tela do WhatsApp.
// Rodar isso no boot fecha essa lacuna sozinho, sem depender da API notar.
export async function restaurarSessoesSalvas() {
  let entradas
  try {
    entradas = await readdir(config.pastaSessoes, { withFileTypes: true })
  } catch {
    return // pasta ainda não existe: nenhuma sessão pra restaurar.
  }

  const prefixo = 'session-'
  const merchantIds = entradas
    .filter((entrada) => entrada.isDirectory() && entrada.name.startsWith(prefixo))
    .map((entrada) => entrada.name.slice(prefixo.length))

  for (const merchantId of merchantIds) {
    console.log(`[zap:${merchantId}] restaurando sessão salva...`)
    conectarSessao(merchantId).catch((erro) =>
      console.error(`[zap:${merchantId}] falha ao restaurar sessão:`, erro.message),
    )
  }
}
