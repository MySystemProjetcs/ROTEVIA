import { useState } from 'react'
import type { ComponentProps } from 'react'
import { Campo } from '@/components/Campo'
import { IconeOlho } from '@/components/icones/IconeOlho'
import { IconeOlhoRiscado } from '@/components/icones/IconeOlhoRiscado'
import { cn } from '@/lib/cn'

type CampoSenhaProps = Omit<ComponentProps<typeof Campo>, 'type'>

// Variação do Campo para senha: mesmo rótulo, mesma borda de erro, mesmo
// espaçamento — só acrescenta o botão de revelar por cima do input. Composto em
// vez de alterado para o Campo continuar servindo os outros formulários sem
// carregar um conceito que só a senha tem.
export function CampoSenha({ className, ...props }: CampoSenhaProps) {
  const [visivel, setVisivel] = useState(false)

  return (
    <div className="relative">
      <Campo
        {...props}
        type={visivel ? 'text' : 'password'}
        // Espaço à direita para o texto da senha não passar por baixo do botão.
        className={cn('pr-11', className)}
      />

      <button
        type="button"
        onClick={() => setVisivel((v) => !v)}
        // aria-pressed diz o estado; o rótulo diz o que o clique faz. Sem isso
        // quem usa leitor de tela não sabe se a senha está exposta na tela.
        aria-pressed={visivel}
        aria-label={visivel ? 'Ocultar senha' : 'Mostrar senha'}
        // Alinhado ao input, não ao bloco: o rótulo em cima e a mensagem de
        // erro embaixo mudam de altura, e o botão não pode acompanhar.
        className="absolute right-1 top-[1.625rem] inline-flex size-9 items-center justify-center rounded-controle text-texto-suave outline-offset-2 transition-colors hover:text-texto focus-visible:outline-2 focus-visible:outline-marca-600"
      >
        {visivel ? <IconeOlhoRiscado className="size-5" /> : <IconeOlho className="size-5" />}
      </button>
    </div>
  )
}
