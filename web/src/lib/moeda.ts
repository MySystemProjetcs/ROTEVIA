// Máscara de moeda com comportamento centavo: o que se digita são centavos
// ("750" vira "7,50", e a vírgula "anda sozinha"). O estado guarda só
// dígitos; toda formatação é derivada — nunca se faz conta em cima de texto
// mascarado.
export function extrairDigitos(texto: string): string {
  return texto
    .replace(/\D/g, '')
    .replace(/^0+(?=\d)/, '')
    .slice(0, 9)
}

export function formatarCentavos(digitos: string): string {
  if (digitos === '') return ''

  return (Number(digitos) / 100).toLocaleString('pt-BR', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })
}

export function centavosParaValor(digitos: string): number | null {
  if (digitos === '') return null

  return Number(digitos) / 100
}
