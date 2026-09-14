import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  // O MapLibre carrega um web worker próprio, e o otimizador de dependências
  // do Vite não o acompanha — o worker some do diretório de deps e o mapa não
  // renderiza. Excluir é a saída indicada pelo próprio aviso do Vite.
  optimizeDeps: {
    exclude: ['maplibre-gl'],
  },
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    // Porta própria do DeliveryHub. A 5173 padrão do Vite é usada por outro
    // projeto na mesma máquina, e strictPort faz falhar em vez de escorregar
    // para outra porta silenciosamente.
    port: 5273,
    strictPort: true,
    // O Vite recusa requisição cujo Host ele não conhece (proteção contra
    // DNS rebinding). Sem isto o túnel do ngrok responde "Blocked request".
    allowedHosts: ['.ngrok-free.app', '.ngrok.app', '.ngrok.io'],
    // A API roda separada. O proxy evita CORS no desenvolvimento e mantém o
    // mesmo caminho relativo que o app empacotado vai usar.
    proxy: {
      '/api': { target: 'http://localhost:5300', changeOrigin: true },
      // SignalR usa WebSocket — ws: true é obrigatório para o upgrade do protocolo.
      '/hubs': { target: 'http://localhost:5300', changeOrigin: true, ws: true },
    },
  },
})
