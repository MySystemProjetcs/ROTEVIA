import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
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
    // A API roda separada. O proxy evita CORS no desenvolvimento e mantém o
    // mesmo caminho relativo que o app empacotado vai usar.
    proxy: {
      '/api': { target: 'http://localhost:5300', changeOrigin: true },
    },
  },
})
