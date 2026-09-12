import { useSessao } from '@/auth/SessaoProvider'
import { Botao } from '@/components/Botao'
import { Etiqueta } from '@/components/Etiqueta'
import { Logo } from '@/components/Logo'
import { Login } from '@/paginas/Login'
import { PainelOperacao } from '@/pedidos/PainelOperacao'

export function App() {
  const { usuario, sair } = useSessao()

  if (!usuario) return <Login />

  return (
    <div className="min-h-dvh bg-superficie-alt">
      <header className="border-b border-borda bg-superficie">
        <div className="mx-auto flex max-w-7xl items-center justify-between gap-4 p-4">
          <div className="flex flex-col gap-0.5">
            <Logo tamanho="compacto" />
            <p className="text-apoio text-texto-suave">{usuario.email}</p>
          </div>

          <div className="flex items-center gap-3">
            <Etiqueta>
              {usuario.papel === 'AdministradorSistema' ? 'Administrador' : 'Loja'}
            </Etiqueta>
            <Botao variante="secundario" onClick={sair}>
              Sair
            </Botao>
          </div>
        </div>
      </header>

      <main className="p-4">
        <PainelOperacao />
      </main>
    </div>
  )
}
