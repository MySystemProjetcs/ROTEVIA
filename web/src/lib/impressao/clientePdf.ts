import type { Comanda, OpcoesImpressao } from './comanda'
import { enderecoFormatado, moeda } from './comanda'

// Em dev/sandbox: renderiza a comanda como HTML dentro de um iframe oculto,
// aciona window.print() e o browser oferece "Salvar como PDF" direto no
// diálogo nativo. Zero dependência extra. Em produção isto é substituído
// pelo cliente ESC/POS (QZ Tray / plugin Capacitor).
export function imprimirComandaPdf(comanda: Comanda, opcoes: OpcoesImpressao = {}): void {
  const html = montarHtml(comanda, opcoes.reimpressao === true)
  const iframe = document.createElement('iframe')
  iframe.setAttribute('aria-hidden', 'true')
  iframe.style.position = 'fixed'
  iframe.style.right = '0'
  iframe.style.bottom = '0'
  iframe.style.width = '0'
  iframe.style.height = '0'
  iframe.style.border = '0'
  document.body.appendChild(iframe)

  const doc = iframe.contentDocument
  if (!doc) {
    document.body.removeChild(iframe)
    return
  }

  doc.open()
  doc.write(html)
  doc.close()

  const limpar = () => {
    // Dá tempo pro print terminar antes de remover o iframe.
    setTimeout(() => document.body.removeChild(iframe), 1_000)
  }

  const janela = iframe.contentWindow
  if (!janela) {
    limpar()
    return
  }

  janela.onafterprint = limpar
  // Pequeno delay garante que o layout está renderizado antes do print.
  setTimeout(() => {
    janela.focus()
    janela.print()
  }, 100)
}

function esc(valor: string | null | undefined): string {
  if (!valor) return ''
  return valor
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
}

function montarHtml(c: Comanda, reimpressao: boolean): string {
  const horario = new Date(c.recebidoEm).toLocaleString('pt-BR', { hour12: false })
  const endereco = enderecoFormatado(c.entrega)

  const itensHtml = c.itens
    .map((item) => {
      const linhaPrincipal = `<div class="item"><span class="qtd">${item.quantidade}x</span> <span class="nome">${esc(item.nome)}</span><span class="preco">${moeda(item.precoTotal)}</span></div>`
      const obs = item.observacoes
        ? `<div class="obs">↳ ${esc(item.observacoes)}</div>`
        : ''
      return linhaPrincipal + obs
    })
    .join('')

  const codigoHtml = c.exigeCodigoDeEntrega
    ? `<div class="bloco destaque"><div class="rotulo">CÓDIGO DE ENTREGA</div><div class="codigo">${esc(c.codigoDeEntrega ?? '— informar com o cliente —')}</div></div>`
    : ''

  const pagamentoLinhas: string[] = []
  if (c.pagamento.descricao) pagamentoLinhas.push(`Forma: ${esc(c.pagamento.descricao)}`)
  if (c.pagamento.valorJaPago != null && c.pagamento.valorJaPago > 0)
    pagamentoLinhas.push(`Já pago: ${moeda(c.pagamento.valorJaPago)}`)
  if (c.pagamento.valorACobrar != null && c.pagamento.valorACobrar > 0)
    pagamentoLinhas.push(`A cobrar: <strong>${moeda(c.pagamento.valorACobrar)}</strong>`)

  const banner = reimpressao
    ? '<div class="banner reimpressao">*** REIMPRESSÃO ***</div>'
    : c.ehTeste
      ? '<div class="banner">PEDIDO DE TESTE — NÃO PRODUZIR</div>'
      : ''

  return `<!doctype html>
<html lang="pt-BR">
<head>
<meta charset="utf-8">
<title>Comanda ${esc(c.numeroExibicao)}</title>
<style>
  @page { size: 80mm auto; margin: 4mm; }
  * { box-sizing: border-box; }
  body { font-family: 'Courier New', Consolas, monospace; font-size: 12px; color: #000; margin: 0; }
  .comanda { width: 72mm; padding: 0; }
  h1 { font-size: 20px; margin: 0 0 2px; letter-spacing: -0.5px; }
  .meta { font-size: 11px; margin-bottom: 6px; }
  .banner { background: #000; color: #fff; text-align: center; padding: 4px; font-weight: bold; margin-bottom: 6px; }
  .banner.reimpressao { background: #fff; color: #000; border: 2px dashed #000; letter-spacing: 2px; }
  .divisor { border-top: 1px dashed #000; margin: 6px 0; }
  .bloco { margin: 6px 0; }
  .rotulo { font-size: 10px; letter-spacing: 1px; text-transform: uppercase; opacity: 0.7; }
  .destaque { border: 2px dashed #000; padding: 6px; text-align: center; }
  .codigo { font-size: 24px; font-weight: bold; letter-spacing: 4px; }
  .item { display: flex; gap: 4px; align-items: baseline; margin-top: 4px; }
  .qtd { font-weight: bold; min-width: 24px; }
  .nome { flex: 1; }
  .preco { font-variant-numeric: tabular-nums; }
  .obs { padding-left: 32px; font-style: italic; font-size: 11px; }
  .total { display: flex; justify-content: space-between; font-size: 14px; font-weight: bold; margin-top: 6px; }
  .pagamento { font-size: 11px; margin-top: 4px; }
  .pagamento div { margin-top: 2px; }
  .endereco { white-space: pre-wrap; font-size: 12px; }
  .rodape { text-align: center; margin-top: 10px; font-size: 10px; opacity: 0.6; }
</style>
</head>
<body>
<div class="comanda">
  ${banner}
  <h1>Pedido #${esc(c.numeroExibicao)}</h1>
  <div class="meta">${esc(c.origem)} · ${horario}</div>

  <div class="divisor"></div>

  <div class="bloco">
    <div class="rotulo">Cliente</div>
    <div><strong>${esc(c.cliente.nome)}</strong></div>
    ${c.cliente.telefone ? `<div>${esc(c.cliente.telefone)}</div>` : ''}
    ${c.cliente.localizador ? `<div>Localizador: ${esc(c.cliente.localizador)}</div>` : ''}
  </div>

  ${
    endereco
      ? `<div class="bloco"><div class="rotulo">Entrega</div><div class="endereco">${esc(endereco)}</div></div>`
      : '<div class="bloco"><div class="rotulo">Retirada no balcão</div></div>'
  }

  <div class="divisor"></div>

  <div class="bloco">
    <div class="rotulo">Itens</div>
    ${itensHtml}
  </div>

  <div class="divisor"></div>

  ${c.taxaEntrega > 0 ? `<div class="pagamento"><div>Taxa de entrega: ${moeda(c.taxaEntrega)}</div></div>` : ''}
  <div class="total"><span>TOTAL</span><span>${moeda(c.valorTotal)}</span></div>

  ${pagamentoLinhas.length > 0 ? `<div class="pagamento">${pagamentoLinhas.map((l) => `<div>${l}</div>`).join('')}</div>` : ''}

  ${codigoHtml}

  <div class="rodape">ToolsDelivery · dev · ${esc(c.pedidoId)}${reimpressao ? ' · reimpressão' : ''}</div>
</div>
<script>window.focus();</script>
</body>
</html>`
}
