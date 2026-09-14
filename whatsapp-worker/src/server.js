import express from 'express'
import cors from 'cors'
import http from 'node:http'
import { config } from './config.js'
import {
  conectarSessao,
  desconectarSessao,
  obterStatus,
  enviarMensagem,
  restaurarSessoesSalvas,
  encerrarTodasAsSessoes,
} from './sessoes.js'

const app = express()
app.use(cors())
app.use(express.json({ limit: '1mb' }))

app.get('/health', (_req, res) => res.json({ ok: true }))

app.post('/api/sessions/:merchantId/connect', async (req, res) => {
  try {
    const resultado = await conectarSessao(req.params.merchantId)
    res.json(resultado)
  } catch (erro) {
    res.status(500).json({ error: erro.message ?? 'Falha ao conectar.' })
  }
})

app.get('/api/sessions/:merchantId/status', (req, res) => {
  res.json(obterStatus(req.params.merchantId))
})

app.post('/api/sessions/:merchantId/disconnect', async (req, res) => {
  try {
    res.json(await desconectarSessao(req.params.merchantId))
  } catch (erro) {
    res.status(500).json({ error: erro.message ?? 'Falha ao desconectar.' })
  }
})

// Dedupe de envio: retentativa de rede da API reaproveita a mesma resposta em
// vez de reenviar ao WhatsApp. Guarda só sucesso — falha pode tentar de novo.
const mensagensVistas = new Map() // `${merchantId}:${chave}` -> { em, status, corpo }
const TTL_IDEMPOTENCIA_MS = 10 * 60 * 1000
setInterval(() => {
  const corte = Date.now() - TTL_IDEMPOTENCIA_MS
  for (const [chave, valor] of mensagensVistas) if (valor.em < corte) mensagensVistas.delete(chave)
}, 60_000).unref()

function guardarIdempotente(chaveDeMapa, status, corpo) {
  mensagensVistas.set(chaveDeMapa, { em: Date.now(), status, corpo })
}

app.post('/api/sessions/:merchantId/messages', async (req, res) => {
  const { to, text } = req.body ?? {}
  if (!to || !text) return res.status(400).json({ error: "Informe 'to' e 'text'." })

  const chaveIdempotencia = req.get('Idempotency-Key')
  const chaveDeMapa = chaveIdempotencia ? `${req.params.merchantId}:${chaveIdempotencia}` : null
  if (chaveDeMapa && mensagensVistas.has(chaveDeMapa)) {
    const encontrada = mensagensVistas.get(chaveDeMapa)
    return res.status(encontrada.status).json({ ...encontrada.corpo, deduplicated: true })
  }

  const responder = (status, corpo) => {
    if (chaveDeMapa && status >= 200 && status < 300) guardarIdempotente(chaveDeMapa, status, corpo)
    return res.status(status).json(corpo)
  }

  try {
    return responder(200, await enviarMensagem(req.params.merchantId, to, text))
  } catch (erro) {
    if (erro.codigo === 'NAO_CONECTADO') return responder(409, { error: erro.message })
    if (erro.codigo === 'NUMERO_INVALIDO' || erro.codigo === 'NAO_ENTREGAVEL') return responder(422, { error: erro.message })
    return responder(500, { error: erro.message ?? 'Falha ao enviar.' })
  }
})

const servidor = http.createServer(app)

servidor.listen(config.porta, () => {
  console.log(`[zap] worker ouvindo em :${config.porta} (API -> ${config.apiBaseUrl})`)
  void restaurarSessoesSalvas()
})

// Sem isto, `Ctrl+C`/restart do processo mata o Node e deixa o Chrome de cada
// sessão órfão segurando o lock do userDataDir — a próxima subida encontra o
// perfil "já em uso" e não consegue restaurar sozinha.
async function encerrarComCalma(sinal) {
  console.log(`[zap] recebido ${sinal}, fechando sessões antes de sair...`)
  await encerrarTodasAsSessoes()
  process.exit(0)
}

process.on('SIGINT', () => void encerrarComCalma('SIGINT'))
process.on('SIGTERM', () => void encerrarComCalma('SIGTERM'))
