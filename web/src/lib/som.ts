import somNotificacaoUrl from '@/assets/notificacao-pedido.mp3'

function tocarUmaVez(): Promise<void> {
  return new Promise((resolve) => {
    const audio = new Audio(somNotificacaoUrl)
    audio.addEventListener('ended', () => resolve(), { once: true })

    // Autoplay bloqueado pelo navegador (aba nunca teve interação, por
    // exemplo) não pode travar o resto do alerta — só desiste em silêncio.
    audio.play().catch(() => resolve())
  })
}

export async function tocarAlertaDePedido(vezes: number): Promise<void> {
  for (let i = 0; i < vezes; i++) {
    await tocarUmaVez()
  }
}
