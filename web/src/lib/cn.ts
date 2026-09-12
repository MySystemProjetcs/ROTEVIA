// Junta classes ignorando falsos. As variantes dos componentes são desenhadas
// para não colidir entre si, então não precisamos de resolução de conflito de
// classe do Tailwind aqui.
export function cn(...classes: Array<string | false | null | undefined>): string {
  return classes.filter(Boolean).join(' ')
}
