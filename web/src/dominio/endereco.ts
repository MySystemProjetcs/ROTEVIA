/** Endereço resolvido a partir do CEP pelo servidor (ViaCEP + geocodificação).
 *
 *  Coordenada nula significa que o CEP existe mas não foi possível localizá-lo
 *  no mapa — acontece de verdade: há rua no Brasil que o OpenStreetMap não
 *  conhece. Nesse caso a tela precisa oferecer o preenchimento manual, senão o
 *  endereço fica impossível de salvar. */
export interface EnderecoResolvido {
  logradouro: string
  bairro: string
  cidade: string
  estado: string
  cep: string
  latitude: number | null
  longitude: number | null
  /** Coordenada veio da rede de segurança por CEP, que erra na casa do
   *  quilômetro. A tela avisa para a pessoa conferir e ajustar se precisar. */
  coordenadaAproximada: boolean
}
