import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { App } from '@/App'
import { SessaoProvider } from '@/auth/SessaoProvider'
import './styles/theme.css'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <SessaoProvider>
      <App />
    </SessaoProvider>
  </StrictMode>,
)
