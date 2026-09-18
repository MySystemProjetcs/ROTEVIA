import { AvatarIniciais } from '@/components/AvatarIniciais'
import { cn } from '@/lib/cn'

interface AvatarUsuarioProps {
  nome: string
  /** Data URL vinda do perfil. Sem foto, caem as iniciais. */
  foto?: string | null
  className?: string
}

// Variação do AvatarIniciais para quem pode ter foto de perfil: mesmo círculo,
// mesmo tamanho, trocando as letras pela imagem quando ela existe. Decorativo
// para leitor de tela — o nome sempre aparece ao lado.
export function AvatarUsuario({ nome, foto, className }: AvatarUsuarioProps) {
  if (!foto) {
    return (
      <AvatarIniciais nome={nome} className={cn('border-borda bg-marca-50 text-marca-600', className)} />
    )
  }

  return (
    <img
      src={foto}
      alt=""
      aria-hidden
      className={cn('size-10 shrink-0 rounded-full border border-borda object-cover', className)}
    />
  )
}
