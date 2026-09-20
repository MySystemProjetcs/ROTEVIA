using DeliveryHub.Domain.Orders;
using DeliveryHub.Domain.SharedKernel;
using DeliveryHub.Infrastructure.Integrations.DiDiFood.Contracts;

namespace DeliveryHub.Infrastructure.Integrations.DiDiFood;

// Anti-Corruption Layer (ACL): traduz os contratos da API do DiDi Food / 99Food
// para a entidade rica do domínio Pedido (CLAUDE.md §4).
// Isolado de iFood: nenhuma classe do iFood é referenciada aqui.
internal static class DiDiFoodOrderMapper
{
    public const string PrefixoIdExterno = "99food-";

    public static Pedido ParaPedido(DiDiOrderModel model, Guid merchantId, DateTimeOffset recebidoEm)
    {
        var idExterno = model.OrderId.ToString();
        var numeroExibicao = model.OrderIndex > 0 ? model.OrderIndex.ToString() : idExterno;

        var cliente = new Cliente(
            Nome: ExtrairNomeCliente(model.ReceiveAddress),
            Telefone: model.ReceiveAddress?.Phone,
            Localizador: null);

        var endereco = MapearEndereco(model.ReceiveAddress);

        // Preços no DiDi chegam na menor unidade da moeda (centavos para BRL/MXN).
        var valorTotal = (model.Price?.RealPayPrice ?? model.Price?.OrderPrice ?? 0) / 100m;
        var taxaEntrega = (model.Price?.DeliveryPrice ?? 0) / 100m;
        var criadoNaOrigem = model.CreateTime > 0
            ? DateTimeOffset.FromUnixTimeSeconds(model.CreateTime)
            : recebidoEm;

        var pagamento = MapearPagamento(model);

        var pedido = Pedido.Receber(
            merchantId,
            idExterno,
            numeroExibicao,
            ehTeste: false,
            cliente,
            endereco,
            valorTotal,
            taxaEntrega,
            criadoNaOrigem,
            recebidoEm,
            pagamento,
            exigeCodigoDeEntrega: false);

        var indice = 1;
        foreach (var item in model.OrderItems)
        {
            var precoUnitario = (item.SkuPrice > 0 ? item.SkuPrice : item.TotalPrice) / 100m;
            var precoTotal = item.TotalPrice / 100m;

            pedido.AdicionarItem(
                indice++,
                item.Name ?? "Item sem nome",
                item.Amount > 0 ? item.Amount : 1,
                "un",
                precoUnitario,
                precoTotal,
                item.Remark);
        }

        AplicarStatusInicial(pedido, model.Status);

        return pedido;
    }

    private static string ExtrairNomeCliente(DiDiCustomerAddressModel? addr)
    {
        if (addr is null) return "Cliente 99Food";

        var nomeCompleto = $"{addr.FirstName} {addr.LastName}".Trim();
        if (!string.IsNullOrWhiteSpace(nomeCompleto)) return nomeCompleto;

        if (!string.IsNullOrWhiteSpace(addr.Name)) return addr.Name.Trim();

        return "Cliente 99Food";
    }

    private static Endereco? MapearEndereco(DiDiCustomerAddressModel? addr)
    {
        if (addr is null) return null;

        double? lat = double.TryParse(addr.PoiLat, System.Globalization.CultureInfo.InvariantCulture, out var parsedLat) ? parsedLat : null;
        double? lng = double.TryParse(addr.PoiLng, System.Globalization.CultureInfo.InvariantCulture, out var parsedLng) ? parsedLng : null;

        var logradouro = addr.PoiAddress ?? addr.PoiDisplayName ?? "Endereço não informado";

        return new Endereco(
            Logradouro: logradouro,
            Numero: addr.HouseNumber ?? "S/N",
            Bairro: string.Empty,
            Cidade: addr.City ?? string.Empty,
            Estado: addr.CountryCode ?? string.Empty,
            Cep: string.Empty,
            Complemento: null,
            Referencia: null,
            Latitude: lat ?? 0.0,
            Longitude: lng ?? 0.0);
    }

    private static Pagamento MapearPagamento(DiDiOrderModel model)
    {
        var total = (model.Price?.RealPayPrice ?? model.Price?.OrderPrice ?? 0) / 100m;

        // pay_type: 1: online, 2: dinheiro, 3: POS (cartão na entrega), 4: carteira DiDi (online)
        var ehOnline = model.PayType == 1 || model.PayType == 4;

        if (ehOnline)
        {
            return new Pagamento(
                ValorJaPago: total,
                ValorACobrar: 0,
                Descricao: "Pagamento Online (99Food)");
        }

        if (model.PayType == 2)
        {
            return new Pagamento(
                ValorJaPago: 0,
                ValorACobrar: total,
                Descricao: "Dinheiro na Entrega");
        }

        if (model.PayType == 3)
        {
            return new Pagamento(
                ValorJaPago: 0,
                ValorACobrar: total,
                Descricao: "Cartão na Entrega (POS)");
        }

        return Pagamento.Indefinido;
    }

    private static void AplicarStatusInicial(Pedido pedido, int statusDidi)
    {
        // Converte status da DiDi para ações de domínio
        // status: 0/1 (recebido), 2 (confirmado/preparo), 3 (pronto), 4 (despachado), 5 (concluido), >=6 (cancelado)
        switch (statusDidi)
        {
            case 2:
                pedido.Confirmar();
                break;
            case 3:
                pedido.Confirmar();
                pedido.IniciarPreparo();
                pedido.MarcarPronto();
                break;
            case 4:
                pedido.Confirmar();
                pedido.IniciarPreparo();
                pedido.MarcarPronto();
                break;
            case 5:
                pedido.Confirmar();
                pedido.IniciarPreparo();
                pedido.MarcarPronto();
                pedido.Concluir();
                break;
            case >= 6:
                pedido.Cancelar();
                break;
        }
    }
}
