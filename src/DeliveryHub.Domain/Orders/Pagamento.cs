namespace DeliveryHub.Domain.Orders;

// Como o pedido foi pago. O domínio não conhece "iFood": o ACL traduz o bloco
// payments do marketplace para cá (CLAUDE.md §4).
//
// ValorACobrar é o que decide tudo — se há pendência, o motoboy cobra na
// entrega. É mais confiável que interpretar o método: um pedido pode vir
// parcialmente pago, com parte no cartão online e o resto em dinheiro.
public sealed record Pagamento(
    decimal ValorJaPago,
    decimal ValorACobrar,
    // Texto pronto para a tela: "Crédito Visa", "PIX", "Dinheiro". Montado no
    // ACL porque a nomenclatura é da origem, não do nosso domínio.
    string Descricao)
{
    public bool PrecisaCobrarNaEntrega => ValorACobrar > 0;

    // Pedido sem informação de pagamento (venda interna antiga, origem que não
    // manda esse dado): tratado como pago, para não inventar cobrança.
    public static Pagamento Indefinido => new(0, 0, "Não informado");
}
