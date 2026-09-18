using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Application.Merchants;

// Onde a loja fica. É a origem de toda entrega: o mapa ancora aqui e a rota do
// motoboy parte daqui, então endereço errado desloca a operação inteira.
//
// A coordenada é opcional de propósito. O OpenStreetMap não cobre todo
// logradouro do Brasil — há rua de verdade que o geocoder simplesmente não
// conhece, e sem escape manual essa loja ficaria para sempre sem posição (ou,
// pior, com a posição de um homônimo em outro bairro).
public sealed record NovoEnderecoDaLoja(
    string Cep,
    string Numero,
    string? Complemento,
    string? Referencia,
    double? Latitude,
    double? Longitude);

public static class EnderecoDaLojaErrors
{
    public static readonly Error CepNaoEncontrado = new(
        "endereco_loja.cep_nao_encontrado",
        "CEP não encontrado.",
        ErrorType.Validation);

    public static readonly Error LojaNaoEncontrada = new(
        "endereco_loja.nao_encontrada",
        "Loja não encontrada.",
        ErrorType.NotFound);

    public static readonly Error CoordenadaIncompleta = new(
        "endereco_loja.coordenada_incompleta",
        "Informe latitude e longitude juntas.",
        ErrorType.Validation);

    public static readonly Error CoordenadaForaDoMundo = new(
        "endereco_loja.coordenada_invalida",
        "Latitude precisa estar entre -90 e 90, e longitude entre -180 e 180.",
        ErrorType.Validation);

    public static readonly Error SemCoordenada = new(
        "endereco_loja.sem_coordenada",
        "Não foi possível localizar este endereço no mapa. Informe a latitude e a longitude.",
        ErrorType.Validation);
}

public interface IDefinirEnderecoDaLoja
{
    Task<Result<EnderecoDaLojaDto>> ExecutarAsync(
        Guid merchantId,
        NovoEnderecoDaLoja novo,
        CancellationToken ct);
}

public sealed class DefinirEnderecoDaLoja : IDefinirEnderecoDaLoja
{
    private readonly IMerchantRepository _merchants;
    private readonly IResolverEndereco _enderecos;

    public DefinirEnderecoDaLoja(IMerchantRepository merchants, IResolverEndereco enderecos)
    {
        _merchants = merchants;
        _enderecos = enderecos;
    }

    public async Task<Result<EnderecoDaLojaDto>> ExecutarAsync(
        Guid merchantId,
        NovoEnderecoDaLoja novo,
        CancellationToken ct)
    {
        var informouUma = novo.Latitude.HasValue ^ novo.Longitude.HasValue;
        if (informouUma)
            return Result.Failure<EnderecoDaLojaDto>(EnderecoDaLojaErrors.CoordenadaIncompleta);

        if (novo.Latitude is { } lat && novo.Longitude is { } lon
            && (lat is < -90 or > 90 || lon is < -180 or > 180))
            return Result.Failure<EnderecoDaLojaDto>(EnderecoDaLojaErrors.CoordenadaForaDoMundo);

        var merchant = await _merchants.ObterPorIdAsync(merchantId, ct);
        if (merchant is null)
            return Result.Failure<EnderecoDaLojaDto>(EnderecoDaLojaErrors.LojaNaoEncontrada);

        var resolvido = await _enderecos.PorCepAsync(novo.Cep, novo.Numero, ct);
        if (resolvido is null)
            return Result.Failure<EnderecoDaLojaDto>(EnderecoDaLojaErrors.CepNaoEncontrado);

        // A coordenada informada à mão vence a do geocoder: quem digitou está
        // olhando o mapa e sabe onde fica a porta da loja.
        var latitude = novo.Latitude ?? resolvido.Latitude;
        var longitude = novo.Longitude ?? resolvido.Longitude;

        // Sem coordenada o endereço não serve para o que ele existe: ancorar o
        // mapa. Recusar aqui é melhor que gravar 0,0 e o pin ir para o Atlântico.
        if (latitude is null || longitude is null)
            return Result.Failure<EnderecoDaLojaDto>(EnderecoDaLojaErrors.SemCoordenada);

        var endereco = new Endereco(
            Logradouro: resolvido.Logradouro,
            Numero: novo.Numero,
            Bairro: resolvido.Bairro,
            Cidade: resolvido.Cidade,
            Estado: resolvido.Estado,
            Cep: resolvido.Cep,
            Complemento: novo.Complemento,
            Referencia: novo.Referencia,
            Latitude: latitude.Value,
            Longitude: longitude.Value);

        merchant.DefinirEndereco(endereco);
        await _merchants.SalvarAsync(ct);

        return Result.Success(new EnderecoDaLojaDto(
            $"{endereco.Logradouro}, {endereco.Numero} - {endereco.Bairro}",
            endereco.Latitude,
            endereco.Longitude));
    }
}
