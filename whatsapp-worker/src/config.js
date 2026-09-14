import 'dotenv/config'

export const config = {
  porta: Number(process.env.PORT ?? 3002),
  apiBaseUrl: (process.env.DOTNET_API_URL ?? 'http://localhost:5300').replace(/\/$/, ''),
  segredoWebhook: process.env.ZAP_WEBHOOK_SECRET ?? '',
  frontendUrl: (process.env.FRONTEND_URL ?? 'http://localhost:5273').replace(/\/$/, ''),
  pastaSessoes: process.env.SESSIONS_DIR ?? './.sessoes-whatsapp',
}
