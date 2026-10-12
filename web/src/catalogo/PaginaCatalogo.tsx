import { Cartao, CartaoCorpo } from '@/components/Cartao'

// Esqueleto da página de Catálogo. O item existia na barra lateral apontando
// para /cardapio sem rota correspondente — clicar não levava a lugar nenhum.
//
// A integração em si (catálogo do iFood) ainda não está implementada: o app
// do projeto tem apenas Order, Events e Merchant habilitados no portal do
// iFood, então as chamadas de Catalog seriam recusadas pelo escopo. Esta
// página diz isso em vez de mostrar uma lista vazia que parece defeito.
export function PaginaCatalogo() {
  return (
    <div className="mx-auto w-full max-w-3xl p-4 sm:p-6">
      <Cartao>
        <CartaoCorpo className="space-y-3">
          <h2 className="text-[15px] font-semibold text-texto">
            Catálogo ainda não conectado
          </h2>

          <p className="text-[13px] leading-relaxed text-texto-suave">
            A sincronização de itens, preços e disponibilidade com as
            plataformas depende de habilitar o módulo de catálogo no portal de
            cada marketplace. Hoje o aplicativo integrado ao iFood tem apenas
            os módulos de pedidos, eventos e loja.
          </p>

          <p className="text-[13px] leading-relaxed text-texto-suave">
            Enquanto isso, o cardápio continua sendo gerido direto no portal do
            iFood e da 99Food.
          </p>
        </CartaoCorpo>
      </Cartao>
    </div>
  )
}
