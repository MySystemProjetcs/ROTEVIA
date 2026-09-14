import { config } from './config.js'

async function postWebhook(caminho, corpo, rotulo) {
  if (!config.apiBaseUrl) return false

  try {
    const resposta = await fetch(`${config.apiBaseUrl}${caminho}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Zap-Secret': config.segredoWebhook,
      },
      body: JSON.stringify(corpo),
    })
    return resposta.ok
  } catch (erro) {
    console.error(`[zap] falha ao notificar ${rotulo}:`, erro.message)
    return false
  }
}

export function notificarMensagemRecebida({ merchantId, de, corpo, externalId, ocorridoEm }) {
  return postWebhook(
    '/api/webhooks/whatsapp',
    { merchantId, de, corpo, externalId, ocorridoEm },
    'mensagem recebida',
  )
}

export function notificarStatusDaSessao({ merchantId, status, qrDataUrl, telefone, erro }) {
  return postWebhook(
    '/api/webhooks/whatsapp/session',
    { merchantId, status, qrDataUrl: qrDataUrl ?? null, telefone: telefone ?? null, erro: erro ?? null },
    'status da sessão',
  )
}
