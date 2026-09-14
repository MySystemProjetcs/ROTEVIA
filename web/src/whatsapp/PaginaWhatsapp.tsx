import { Botao } from '@/components/Botao'
import { Cartao, CartaoCorpo } from '@/components/Cartao'
import { Etiqueta } from '@/components/Etiqueta'
import { IconeWhatsApp } from '@/components/icones/IconeWhatsApp'
import { useConexaoWhatsapp } from './useConexaoWhatsapp'

export function PaginaWhatsapp() {
  const { dados, carregando, erro, conectando, conectar, desconectar } = useConexaoWhatsapp()

  return (
    <div className="mx-auto flex max-w-xl flex-col gap-6">
      <div>
        <h1 className="text-titulo font-semibold text-texto">WhatsApp</h1>
        <p className="text-apoio text-texto-suave">
          Conecte o número da loja para enviar convites de motoboy automaticamente.
        </p>
      </div>

      {erro && (
        <Cartao className="border-perigo">
          <CartaoCorpo>{erro}</CartaoCorpo>
        </Cartao>
      )}

      <Cartao elevacao="elevada">
        <CartaoCorpo>
          <div className="flex flex-col items-center gap-4 py-8 text-center">
            {carregando ? (
              <p className="text-apoio text-texto-suave">Carregando...</p>
            ) : dados?.status === 'Conectado' ? (
              <>
                <IconeWhatsApp className="size-12 text-sucesso" />
                <Etiqueta tom="sucesso">{`Conectado${dados.telefone ? ` — ${dados.telefone}` : ''}`}</Etiqueta>
                <Botao variante="secundario" onClick={desconectar}>
                  Desconectar
                </Botao>
              </>
            ) : dados?.status === 'AguardandoLeituraDoQr' && dados.qrCodeBase64 ? (
              <>
                {/* O worker já devolve uma data URL pronta (QRCode.toDataURL) — sem
                    prefixo pra montar aqui, diferente de quando era print do DOM. */}
                <img
                  src={dados.qrCodeBase64}
                  alt="QR code para conectar o WhatsApp"
                  className="size-48 rounded-controle border border-borda"
                />
                <p className="max-w-sm text-apoio text-texto-suave">
                  Abra o WhatsApp no celular da loja → Aparelhos conectados → Conectar um
                  aparelho, e escaneie o código acima. Ele se renova sozinho a cada poucos
                  segundos.
                </p>
              </>
            ) : (
              <>
                <IconeWhatsApp className="size-12 text-texto-fraco" />
                <Etiqueta tom="neutro">Desconectado</Etiqueta>
                <Botao onClick={conectar} carregando={conectando}>
                  Conectar
                </Botao>
              </>
            )}
          </div>
        </CartaoCorpo>
      </Cartao>
    </div>
  )
}
