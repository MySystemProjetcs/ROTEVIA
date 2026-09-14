namespace DeliveryHub.Application.Merchants;

// O ponto fixo do mapa: é daqui que toda entrega sai. Só o que a tela precisa
// para desenhar o pin — sem CEP, complemento ou referência, que não ajudam a
// posicionar nada.
public sealed record EnderecoDaLojaDto(
    string Resumo,
    double Latitude,
    double Longitude);

public interface IObterEnderecoDaLoja
{
    // Nulo quando a loja ainda não teve endereço informado — a tela ancora o
    // mapa no motoboy nesse caso, em vez de mostrar um pin errado.
    Task<EnderecoDaLojaDto?> ExecutarAsync(Guid merchantId, CancellationToken ct);
}
