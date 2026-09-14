import { Etiqueta } from '@/components/Etiqueta'
import type { EstadoGps } from './useEnviarPosicao'

// O motoboy precisa saber se a loja está conseguindo acompanhá-lo. Silêncio
// aqui seria pior que aviso: ele acharia que está sendo rastreado quando não
// está.
const POR_ESTADO: Record<Exclude<EstadoGps, 'inativo'>, { tom: 'sucesso' | 'alerta'; texto: string }> = {
  emitindo: { tom: 'sucesso', texto: 'GPS ativo' },
  negado: { tom: 'alerta', texto: 'GPS bloqueado' },
  // Sem HTTPS o navegador nem expõe a API de geolocalização.
  indisponivel: { tom: 'alerta', texto: 'GPS indisponível' },
  erro: { tom: 'alerta', texto: 'GPS sem sinal' },
}

export function EtiquetaGps({ estado }: { estado: EstadoGps }) {
  if (estado === 'inativo') return null

  const { tom, texto } = POR_ESTADO[estado]

  return <Etiqueta tom={tom}>{texto}</Etiqueta>
}
