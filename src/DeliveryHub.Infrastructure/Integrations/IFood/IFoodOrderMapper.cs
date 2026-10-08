using DeliveryHub.Domain.Orders;
using DeliveryHub.Domain.SharedKernel;
using DeliveryHub.Infrastructure.Integrations.IFood.Contracts;

namespace DeliveryHub.Infrastructure.Integrations.IFood;

// Camada anticorrupção: é aqui, e só aqui, que o formato do iFood vira domínio.
// Nenhum tipo de Contracts pode atravessar esta fronteira (CLAUDE.md §4).
internal static class IFoodOrderMapper
{
    // merchantId é o nosso tenant, já resolvido a partir do merchant.id do
    // iFood — o payload não sabe quem somos nós.
    public static Pedido ParaPedido(IFoodOrderDetails origem, Guid merchantId, DateTimeOffset recebidoEm)
    {
        var pedido = Pedido.Receber(
            merchantId: merchantId,
            idExterno: origem.Id.ToString(),
            numeroExibicao: origem.DisplayId,
            ehTeste: origem.IsTest,
            cliente: MapearCliente(origem.Customer),
            enderecoEntrega: MapearEndereco(origem.Delivery),
            valorTotal: origem.Total.OrderAmount,
            taxaEntrega: origem.Total.DeliveryFee,
            criadoNaOrigemEm: origem.CreatedAt,
            recebidoEm: recebidoEm,
            pagamento: MapearPagamento(origem.Payments),
            // Entrega feita pela frota da própria loja é onde o código de
            // confirmação existe. Pedido entregue pelo iFood não tem esse
            // passo — quem valida lá é o entregador deles.
            exigeCodigoDeEntrega: origem.Delivery?.DeliveredBy == "MERCHANT",
            // pickupCode vem do próprio pedido no detalhe do iFood: é o
            // número que o cliente informa na porta e que o motoboy digita
            // para confirmar via verifyDeliveryCode. Antes era ignorado,
            // então o motoboy via "aguardando código" sem saber qual era.
            codigoDeEntrega: origem.Delivery?.DeliveredBy == "MERCHANT"
                ? origem.Delivery?.PickupCode
                : null);

        foreach (var item in origem.Items)
        {
            pedido.AdicionarItem(
                indice: item.Index,
                nome: item.Name,
                quantidade: item.Quantity,
                unidade: item.Unit,
                precoUnitario: item.UnitPrice,
                precoTotal: item.TotalPrice,
                observacoes: item.Observations);
        }

        return pedido;
    }

    // documentNumber e documentType existem no payload e são descartados de
    // propósito: entrega não precisa de CPF (LGPD, minimização).
    // "pending" é o que manda: é quanto ainda falta receber, e cobre o caso de
    // pagamento parcial (parte no cartão online, resto em dinheiro na porta).
    // Deduzir isso pelo método daria errado nesse caso.
    private static Pagamento MapearPagamento(IFoodPayments origem)
    {
        var descricao = origem.Methods.Count == 0
            ? "Não informado"
            : string.Join(" + ", origem.Methods.Select(Descrever));

        return new Pagamento(origem.Prepaid, origem.Pending, descricao);
    }

    private static string Descrever(IFoodPaymentMethod metodo)
    {
        var nome = metodo.Method.ToUpperInvariant() switch
        {
            "CREDIT" => "Crédito",
            "DEBIT" => "Débito",
            "PIX" => "PIX",
            "CASH" => "Dinheiro",
            "MEAL_VOUCHER" => "Vale-refeição",
            "FOOD_VOUCHER" => "Vale-alimentação",
            "DIGITAL_WALLET" => "Carteira digital",
            // Método novo do iFood não pode virar texto vazio na tela do
            // lojista: mostra o código cru, que ainda é informação.
            var outro => outro,
        };

        // A bandeira só aparece quando existe: "Crédito Visa" ajuda a conferir
        // a maquininha, "Dinheiro Visa" seria absurdo.
        return metodo.Card?.Brand is { Length: > 0 } bandeira ? $"{nome} {bandeira}" : nome;
    }

    private static Cliente MapearCliente(IFoodCustomer origem) =>
        new(origem.Name, origem.Phone?.Number, origem.Phone?.Localizer);

    private static Endereco? MapearEndereco(IFoodDeliveryInformation? entrega)
    {
        if (entrega is null)
            return null;

        var endereco = entrega.DeliveryAddress;

        return new Endereco(
            Logradouro: endereco.StreetName,
            Numero: endereco.StreetNumber,
            Bairro: endereco.Neighborhood ?? string.Empty,
            Cidade: endereco.City,
            Estado: endereco.State,
            Cep: endereco.PostalCode,
            Complemento: endereco.Complement,
            Referencia: endereco.Reference,
            Latitude: endereco.Coordinates.Latitude,
            Longitude: endereco.Coordinates.Longitude);
    }
}
