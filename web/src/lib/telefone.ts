// Só exibição: "5511992345678" vira "(11) 99234-5678". Formato
// desconhecido volta como veio — nunca quebrar a tela por causa de máscara.
export function formatarTelefoneExibicao(valor: string): string {
  let digitos = valor.replace(/\D/g, '')
  if (digitos.startsWith('55')) digitos = digitos.slice(2)

  if (digitos.length === 11)
    return `(${digitos.slice(0, 2)}) ${digitos.slice(2, 7)}-${digitos.slice(7)}`

  if (digitos.length === 10)
    return `(${digitos.slice(0, 2)}) ${digitos.slice(2, 6)}-${digitos.slice(6)}`

  return valor
}

// O campo pede só DDD+número — ninguém deveria precisar lembrar de digitar o
// 55. Sem o código do país o WhatsApp não reconhece o contato e a mensagem
// simplesmente não chega, sem erro nenhum aparente (foi exatamente o que
// aconteceu no primeiro teste de convite).
export function normalizarTelefoneBr(valor: string): string {
  const digitos = valor.replace(/\D/g, '')
  if (digitos.startsWith('55') && digitos.length >= 12) return digitos
  return `55${digitos}`
}
