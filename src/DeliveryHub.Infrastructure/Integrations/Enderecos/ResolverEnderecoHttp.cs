using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DeliveryHub.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace DeliveryHub.Infrastructure.Integrations.Enderecos;

// ViaCEP para o endereço e Nominatim (OpenStreetMap) para a coordenada. Os dois
// são gratuitos e sem chave — mesma razão do §3 preferir OSRM self-hosted a
// Google/Mapbox: custo de API por requisição destrói margem em SMB.
internal sealed class ResolverEnderecoHttp : IResolverEndereco
{
    private readonly HttpClient _http;
    private readonly ILogger<ResolverEnderecoHttp> _logger;

    public ResolverEnderecoHttp(HttpClient http, ILogger<ResolverEnderecoHttp> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<EnderecoResolvido?> PorCepAsync(string cep, string numero, CancellationToken ct)
    {
        var somenteDigitos = new string(cep.Where(char.IsDigit).ToArray());
        if (somenteDigitos.Length != 8)
            return null;

        var viaCep = await BuscarNoViaCepAsync(somenteDigitos, ct);
        if (viaCep is null || viaCep.Erro)
            return null;

        var coordenada = await GeocodificarAsync(viaCep, numero, ct);

        return new EnderecoResolvido(
            viaCep.Logradouro ?? string.Empty,
            viaCep.Bairro ?? string.Empty,
            viaCep.Localidade ?? string.Empty,
            viaCep.Uf ?? string.Empty,
            somenteDigitos,
            coordenada?.Latitude,
            coordenada?.Longitude);
    }

    private async Task<RespostaViaCep?> BuscarNoViaCepAsync(string cep, CancellationToken ct)
    {
        try
        {
            return await _http.GetFromJsonAsync<RespostaViaCep>(
                $"https://viacep.com.br/ws/{cep}/json/", ct);
        }
        catch (Exception ex)
        {
            // Serviço fora não pode derrubar o lançamento do pedido: quem chama
            // trata o nulo pedindo o endereço na mão.
            _logger.LogWarning(ex, "Falha ao consultar o CEP {Cep} no ViaCEP.", cep);
            return null;
        }
    }

    // Duas tentativas, da mais precisa para a menos. A segunda derruba número e
    // bairro de propósito: o OpenStreetMap não tem todo número predial do
    // Brasil, e o bairro do ViaCEP frequentemente discorda do que o OSM
    // registra — "Vila Damaceno" no ViaCEP é "Jardim Ângela" no OSM, e mandar o
    // bairro junto faz a busca voltar vazia mesmo com a rua mapeada.
    //
    // Sem esse segundo tiro o pedido é gravado sem coordenada e some do mapa; a
    // rua certa já resolve para quem está acompanhando a entrega.
    private async Task<(double Latitude, double Longitude)?> GeocodificarAsync(
        RespostaViaCep endereco, string numero, CancellationToken ct)
    {
        // Só os dígitos: o lojista digita "#8989", "8989 fundos", "s/n", e
        // qualquer um desses no meio da consulta zera o resultado.
        var numeroLimpo = new string(numero.Where(char.IsDigit).ToArray());

        var precisa = $"{endereco.Logradouro} {numeroLimpo}, {endereco.Bairro}, {endereco.Localidade}, {endereco.Uf}, Brasil";
        var apenasRua = $"{endereco.Logradouro}, {endereco.Localidade}, {endereco.Uf}, Brasil";

        if (numeroLimpo.Length > 0)
        {
            var exato = await BuscarCoordenadaAsync(precisa, endereco.Cep, ct);
            if (exato is not null)
                return exato;

            _logger.LogInformation(
                "CEP {Cep} não casou com número e bairro; tentando só o logradouro.", endereco.Cep);
        }

        return await BuscarCoordenadaAsync(apenasRua, endereco.Cep, ct);
    }

    private async Task<(double Latitude, double Longitude)?> BuscarCoordenadaAsync(
        string enderecoEscrito, string? cep, CancellationToken ct)
    {
        // Nominatim pede User-Agent identificando a aplicação; sem ele a
        // requisição é recusada.
        var consulta = Uri.EscapeDataString(enderecoEscrito);

        try
        {
            var achados = await _http.GetFromJsonAsync<IReadOnlyList<RespostaNominatim>>(
                $"https://nominatim.openstreetmap.org/search?format=json&limit=1&q={consulta}", ct);

            var primeiro = achados?.FirstOrDefault();
            if (primeiro is null
                || !double.TryParse(primeiro.Lat, System.Globalization.CultureInfo.InvariantCulture, out var lat)
                || !double.TryParse(primeiro.Lon, System.Globalization.CultureInfo.InvariantCulture, out var lon))
                return null;

            return (lat, lon);
        }
        catch (Exception ex)
        {
            // Sem coordenada o pedido ainda é válido — só não vai para o mapa.
            _logger.LogWarning(ex, "Falha ao geocodificar o CEP {Cep}.", cep);
            return null;
        }
    }

    private sealed record RespostaViaCep(
        [property: JsonPropertyName("cep")] string? Cep,
        [property: JsonPropertyName("logradouro")] string? Logradouro,
        [property: JsonPropertyName("bairro")] string? Bairro,
        [property: JsonPropertyName("localidade")] string? Localidade,
        [property: JsonPropertyName("uf")] string? Uf,
        [property: JsonPropertyName("erro")] bool Erro);

    private sealed record RespostaNominatim(
        [property: JsonPropertyName("lat")] string? Lat,
        [property: JsonPropertyName("lon")] string? Lon);
}
