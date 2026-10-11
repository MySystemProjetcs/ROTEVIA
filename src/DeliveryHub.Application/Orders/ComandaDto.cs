using DeliveryHub.Domain.Orders;

namespace DeliveryHub.Application.Orders;

// Alias documental: o que trafega pelo SignalR e pela API é exatamente o
// snapshot persistido em pedidos.receipt_data. Sem DTOs paralelos — o cliente
// web lê o mesmo formato que o banco guarda, com a mesma versão de esquema.
public static class ComandaBuilder
{
    public static PedidoReceiptSnapshot Montar(Pedido pedido)
    {
        var endereco = pedido.EnderecoEntrega is null
            ? null
            : new ReceiptEntrega(
                pedido.EnderecoEntrega.Logradouro,
                pedido.EnderecoEntrega.Numero,
                pedido.EnderecoEntrega.Complemento,
                pedido.EnderecoEntrega.Bairro,
                pedido.EnderecoEntrega.Cidade,
                pedido.EnderecoEntrega.Estado,
                pedido.EnderecoEntrega.Cep,
                pedido.EnderecoEntrega.Referencia);

        var itens = pedido.Itens
            .OrderBy(i => i.Indice)
            .Select(i => new ReceiptItem(
                i.Indice, i.Nome, i.Quantidade, i.Unidade,
                i.PrecoUnitario, i.PrecoTotal, i.Observacoes))
            .ToList();

        var origem = pedido.IdExterno.StartsWith(Pedido.PrefixoOrigemLocal, StringComparison.Ordinal)
            ? "Interno"
            : "IFood";

        return new PedidoReceiptSnapshot(
            SchemaVersion: PedidoReceiptSnapshot.VersaoAtual,
            PedidoId: pedido.Id,
            NumeroExibicao: pedido.NumeroExibicao,
            EhTeste: pedido.EhTeste,
            Origem: origem,
            IdExterno: pedido.IdExterno,
            MerchantId: pedido.MerchantId,
            CriadoEm: pedido.CriadoNaOrigemEm,
            RecebidoEm: pedido.RecebidoEm,
            Cliente: new ReceiptCliente(pedido.Cliente.Nome, pedido.Cliente.Telefone, pedido.Cliente.Localizador),
            Entrega: endereco,
            Itens: itens,
            Pagamento: new ReceiptPagamento(
                pedido.Pagamento.Descricao,
                pedido.Pagamento.ValorACobrar,
                pedido.Pagamento.ValorJaPago),
            ValorTotal: pedido.ValorTotal,
            TaxaEntrega: pedido.TaxaEntrega,
            CodigoDeEntrega: pedido.CodigoDeEntrega,
            ExigeCodigoDeEntrega: pedido.ExigeCodigoDeEntrega);
    }
}
