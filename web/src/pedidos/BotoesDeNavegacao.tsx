import { Botao } from '@/components/Botao'
import type { Pedido } from '@/dominio/pedido'

// Coordenada é sempre melhor que texto: leva ao ponto exato, sem depender de o
// app interpretar o endereço. Sem ela, cai na busca por endereço mesmo.
function destinos(pedido: Pedido): { maps: string; waze: string } | null {
  if (pedido.enderecoLatitude != null && pedido.enderecoLongitude != null) {
    const ponto = `${pedido.enderecoLatitude},${pedido.enderecoLongitude}`

    return {
      maps: `https://www.google.com/maps/dir/?api=1&destination=${ponto}&travelmode=driving`,
      waze: `https://waze.com/ul?ll=${ponto}&navigate=yes`,
    }
  }

  if (!pedido.enderecoResumido) return null

  const busca = encodeURIComponent(pedido.enderecoResumido)

  return {
    maps: `https://www.google.com/maps/dir/?api=1&destination=${busca}&travelmode=driving`,
    waze: `https://waze.com/ul?q=${busca}&navigate=yes`,
  }
}

// Só enquanto o motoboy está indo: aceito (a caminho da loja) e em rota (a
// caminho do cliente). Antes disso ele nem pegou o pedido; depois, já chegou.
export function BotoesDeNavegacao({ pedido }: { pedido: Pedido }) {
  if (pedido.status !== 'Aceito' && pedido.status !== 'EmRota') return null

  const alvo = destinos(pedido)
  if (!alvo) return null

  return (
    <div className="flex gap-2">
      {/* rel="noreferrer" porque target="_blank" sem isso expõe window.opener
          à página aberta. */}
      <Botao
        variante="secundario"
        larguraTotal
        onClick={() => window.open(alvo.maps, '_blank', 'noopener,noreferrer')}
      >
        Google Maps
      </Botao>
      <Botao
        variante="secundario"
        larguraTotal
        onClick={() => window.open(alvo.waze, '_blank', 'noopener,noreferrer')}
      >
        Waze
      </Botao>
    </div>
  )
}
