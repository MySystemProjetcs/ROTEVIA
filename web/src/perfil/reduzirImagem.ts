// Foto de perfil vai para o banco como base64, então o tamanho que sai daqui é
// o tamanho que ocupa numa coluna para sempre. Uma foto de celular tem alguns
// MB; recortada em 256px quadrados ela cai para dezenas de KB — e 256 é o dobro
// do maior avatar que a interface desenha, o que cobre tela retina.
const LADO = 256
const QUALIDADE = 0.82

export async function reduzirImagem(arquivo: File): Promise<string> {
  const bitmap = await createImageBitmap(arquivo)

  try {
    // Recorte central quadrado: o avatar é redondo, então esticar a foto para
    // caber deformaria o rosto.
    const lado = Math.min(bitmap.width, bitmap.height)
    const x = (bitmap.width - lado) / 2
    const y = (bitmap.height - lado) / 2

    const canvas = document.createElement('canvas')
    canvas.width = LADO
    canvas.height = LADO

    const contexto = canvas.getContext('2d')
    if (!contexto) throw new Error('canvas indisponível')

    contexto.drawImage(bitmap, x, y, lado, lado, 0, 0, LADO, LADO)

    // WebP comprime bem melhor que JPEG nesse tamanho; o domínio aceita os dois.
    return canvas.toDataURL('image/webp', QUALIDADE)
  } finally {
    bitmap.close()
  }
}
