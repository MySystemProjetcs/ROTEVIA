import { IconeMenu } from '@/components/icones/IconeMenu'
import { useBarraLateral } from './contexto'

// Um botão para os dois tamanhos: no celular abre e fecha a gaveta, no desktop
// alterna entre coluna e trilho de ícones. O `alternar` do contexto é quem sabe
// qual dos dois estados mexer.
export function GatilhoDaBarra() {
  const { alternar, expandida, abertaNoCelular, ehCelular } = useBarraLateral()
  const mostrando = ehCelular ? abertaNoCelular : expandida

  return (
    <button
      type="button"
      onClick={alternar}
      aria-label={mostrando ? 'Recolher menu' : 'Expandir menu'}
      aria-expanded={mostrando}
      aria-controls="barra-lateral"
      // A dica lembra o atalho: quem usa o painel o dia todo não deveria
      // precisar caçar o botão.
      title={`${mostrando ? 'Recolher' : 'Expandir'} menu (⌘B)`}
      className="inline-flex size-9 shrink-0 items-center justify-center rounded-controle text-texto-suave outline-offset-2 transition-colors hover:bg-superficie-alt hover:text-texto focus-visible:outline-2 focus-visible:outline-marca-600"
    >
      <IconeMenu className="size-6" />
    </button>
  )
}
