namespace DeliveryHub.Domain.Orders;

// Snapshot imutável dos dados que vão para a comanda no momento em que o
// pedido foi recebido. Persistido como JSONB em pedidos.receipt_data e
// recuperado na reimpressão sem reconsultar a API da plataforma de origem.
// Preserva valores e nomes do momento do pedido mesmo que o cadastro de
// produtos/preços mude depois (plano §3).
public sealed record PedidoReceiptSnapshot(
    int SchemaVersion,
    Guid PedidoId,
    string NumeroExibicao,
    bool EhTeste,
    string Origem,
    string IdExterno,
    Guid MerchantId,
    DateTimeOffset CriadoEm,
    DateTimeOffset RecebidoEm,
    ReceiptCliente Cliente,
    ReceiptEntrega? Entrega,
    IReadOnlyList<ReceiptItem> Itens,
    ReceiptPagamento Pagamento,
    decimal ValorTotal,
    decimal TaxaEntrega,
    string? CodigoDeEntrega,
    bool ExigeCodigoDeEntrega)
{
    // Versão atual do esquema. Incremente ao mudar o formato do JSONB de um
    // modo incompatível e trate migração na leitura.
    public const int VersaoAtual = 1;
}

public sealed record ReceiptCliente(string Nome, string? Telefone, string? Localizador);

public sealed record ReceiptEntrega(
    string? Logradouro,
    string? Numero,
    string? Complemento,
    string? Bairro,
    string? Cidade,
    string? Estado,
    string? Cep,
    string? Referencia);

public sealed record ReceiptItem(
    int Indice,
    string Nome,
    decimal Quantidade,
    string? Unidade,
    decimal PrecoUnitario,
    decimal PrecoTotal,
    string? Observacoes);

public sealed record ReceiptPagamento(
    string? Descricao,
    decimal? ValorACobrar,
    decimal? ValorJaPago);
