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
            recebidoEm: recebidoEm);

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
