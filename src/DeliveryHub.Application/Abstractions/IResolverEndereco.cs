namespace DeliveryHub.Application.Abstractions;

// Endereço resolvido a partir do CEP, já com coordenada. Coordenada nula
// significa que o CEP existe mas não foi possível localizá-lo no mapa — o
// pedido ainda pode ser criado, só não aparece georreferenciado.
public sealed record EnderecoResolvido(
    string Logradouro,
    string Bairro,
    string Cidade,
    string Estado,
    string Cep,
    double? Latitude,
    double? Longitude);

// Uma porta só, embora a implementação use dois serviços (CEP e geocodificação):
// para quem chama, a pergunta é uma — "que endereço é este CEP, e onde fica".
public interface IResolverEndereco
{
    // Nulo quando o CEP não existe.
    Task<EnderecoResolvido?> PorCepAsync(string cep, string numero, CancellationToken ct);
}
