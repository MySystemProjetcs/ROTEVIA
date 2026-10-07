using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DeliveryHub.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeliveryHub.Infrastructure.Integrations.Enderecos;

// ViaCEP resolve o endereço textual a partir do CEP — isso nunca muda, é
// gratuito e autoritativo. A coordenada é que passa por uma cascata:
//
//   Google Geocoding (se houver chave) → Nominatim x3 → AwesomeAPI por CEP
//
// Google é pago, mas aqui é usado uma vez por cadastro de loja — não no
// roteamento por entrega (esse continua OSRM self-hosted, §3: "custo de API
// por requisição destrói margem em SMB"). Nesse volume baixíssimo, a precisão
// maior do Google (cobertura de rua no Brasil muito além do OpenStreetMap)
// compensa o custo. Sem chave configurada, o resolvedor nem tenta o Google e
// cai direto na cadeia gratuita de sempre — nenhuma credencial é obrigatória.
internal sealed class ResolverEnderecoHttp : IResolverEndereco
{
    private readonly HttpClient _http;
    private readonly ILogger<ResolverEnderecoHttp> _logger;
    private readonly GoogleMapsOptions _googleMaps;

    public ResolverEnderecoHttp(HttpClient http, ILogger<ResolverEnderecoHttp> logger, IOptions<GoogleMapsOptions> googleMaps)
    {
        _http = http;
        _logger = logger;
        _googleMaps = googleMaps.Value;
    }

    public async Task<EnderecoResolvido?> PorCepAsync(string cep, string numero, CancellationToken ct)
    {
        var somenteDigitos = new string(cep.Where(char.IsDigit).ToArray());
        if (somenteDigitos.Length != 8)
            return null;

        var viaCep = await BuscarNoViaCepAsync(somenteDigitos, ct);
        if (viaCep is null || viaCep.Erro)
            return null;

        var coordenada = await GeocodificarAsync(viaCep, numero, somenteDigitos, ct);

        return new EnderecoResolvido(
            viaCep.Logradouro ?? string.Empty,
            viaCep.Bairro ?? string.Empty,
            viaCep.Localidade ?? string.Empty,
            viaCep.Uf ?? string.Empty,
            somenteDigitos,
            coordenada?.Latitude,
            coordenada?.Longitude,
            coordenada?.Aproximada ?? false);
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

    // Três tentativas, da mais precisa para a menos, parando na primeira que
    // acerta. A do meio é a que mais salva: o CEP delimita um trecho de rua,
    // então cai perto da porta, enquanto o logradouro inteiro pode ter
    // quilômetros — numa avenida longa o ponto sai a centenas de metros.
    //
    // A primeira derruba nada e a última derruba número e bairro de propósito:
    // o OpenStreetMap não tem todo número predial do Brasil, e o bairro do
    // ViaCEP frequentemente discorda do que o OSM registra — "Vila Damaceno" no
    // ViaCEP é "Jardim Ângela" no OSM, e mandar o bairro junto faz a busca
    // voltar vazia mesmo com a rua mapeada.
    private async Task<(double Latitude, double Longitude, bool Aproximada)?> GeocodificarAsync(
        RespostaViaCep endereco, string numero, string cepDigitos, CancellationToken ct)
    {
        // Só os dígitos: o lojista digita "#8989", "8989 fundos", "s/n", e
        // qualquer um desses no meio da consulta zera o resultado.
        var numeroLimpo = new string(numero.Where(char.IsDigit).ToArray());

        if (!string.IsNullOrWhiteSpace(_googleMaps.ApiKey))
        {
            var peloGoogle = await GeocodificarPorGoogleAsync(endereco, numeroLimpo, cepDigitos, ct);
            if (peloGoogle is not null)
                return peloGoogle;

            _logger.LogInformation(
                "Google Geocoding não resolveu o CEP {Cep}; caindo para a cadeia gratuita.", cepDigitos);
        }

        var tentativas = new List<(string Estrategia, string Parametros)>();

        if (numeroLimpo.Length > 0)
        {
            var precisa = $"{endereco.Logradouro} {numeroLimpo}, {endereco.Bairro}, {endereco.Localidade}, {endereco.Uf}, Brasil";
            tentativas.Add(("logradouro com número", $"q={Uri.EscapeDataString(precisa)}"));
        }

        if (!string.IsNullOrWhiteSpace(endereco.Cep))
            tentativas.Add(("CEP", $"country=Brazil&postalcode={Uri.EscapeDataString(endereco.Cep)}"));

        var apenasRua = $"{endereco.Logradouro}, {endereco.Localidade}, {endereco.Uf}, Brasil";
        tentativas.Add(("somente logradouro", $"q={Uri.EscapeDataString(apenasRua)}"));

        for (var i = 0; i < tentativas.Count; i++)
        {
            // O Nominatim limita a uma requisição por segundo e responde 429 a
            // quem atropela. A pausa só existe entre tentativas: quando a
            // primeira acerta — o caso comum — o formulário não espera nada.
            if (i > 0)
                await Task.Delay(TimeSpan.FromSeconds(1.1), ct);

            var achado = await BuscarCoordenadaAsync(tentativas[i].Parametros, endereco.Cep, ct);
            if (achado is not null)
                return (achado.Value.Latitude, achado.Value.Longitude, false);

            _logger.LogInformation(
                "Geocodificação do CEP {Cep} por {Estrategia} não retornou resultado.",
                endereco.Cep, tentativas[i].Estrategia);
        }

        // Rede de segurança: há rua no Brasil que o OpenStreetMap simplesmente
        // não tem (a Rua Romão Puiggari, em São Paulo, é uma delas). Sem isto o
        // endereço fica impossível de salvar, porque a loja precisa de um ponto
        // para ancorar o mapa da operação.
        var porCep = await CoordenadaPorCepAsync(cepDigitos, ct);

        return porCep is null
            ? null
            : (porCep.Value.Latitude, porCep.Value.Longitude, true);
    }

    // Fonte primária quando há chave configurada. Cobertura de rua no Brasil
    // é muito maior que o OpenStreetMap, inclusive para ruas novas que o
    // Nominatim nunca indexou (ex.: Rua Romão Puiggari, Vila Moraes/SP — o
    // caso real que motivou esta cascata toda). O `location_type` da resposta
    // diz se o ponto é do prédio (ROOFTOP/RANGE_INTERPOLATED) ou só uma
    // aproximação da região (GEOMETRIC_CENTER/APPROXIMATE) — é o próprio
    // Google quem decide se marca como aproximada, não um palpite nosso.
    private async Task<(double Latitude, double Longitude, bool Aproximada)?> GeocodificarPorGoogleAsync(
        RespostaViaCep endereco, string numeroLimpo, string cepDigitos, CancellationToken ct)
    {
        var enderecoTexto = numeroLimpo.Length > 0
            ? $"{endereco.Logradouro} {numeroLimpo}"
            : endereco.Logradouro ?? string.Empty;

        // Tentativa 1: logradouro + número, restrito ao CEP exato — desambigua
        // ruas homônimas em cidades diferentes.
        var resultado = await ChamarGoogleAsync(
            enderecoTexto, $"postal_code:{cepDigitos}|country:BR", cepDigitos, ct);

        // Tentativa 2: sem o filtro de CEP, endereço por extenso — o
        // postal_code que o Google conhece às vezes não bate exatamente com o
        // dos Correios, e aí o filtro da tentativa 1 zera um resultado que
        // existiria sem ele.
        resultado ??= await ChamarGoogleAsync(
            $"{enderecoTexto}, {endereco.Bairro}, {endereco.Localidade} - {endereco.Uf}, {cepDigitos}, Brasil",
            "country:BR", cepDigitos, ct);

        return resultado;
    }

    private async Task<(double Latitude, double Longitude, bool Aproximada)?> ChamarGoogleAsync(
        string endereco, string componentes, string cepDigitos, CancellationToken ct)
    {
        try
        {
            // A chave só entra na URL da requisição — nunca em texto que vá
            // para o log (CLAUDE.md §7: nunca logar secret).
            var url = "https://maps.googleapis.com/maps/api/geocode/json"
                + $"?address={Uri.EscapeDataString(endereco)}"
                + $"&components={Uri.EscapeDataString(componentes)}"
                + "&region=br"
                + $"&key={_googleMaps.ApiKey}";

            using var resposta = await _http.GetAsync(url, ct);

            if (!resposta.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Google Geocoding recusou a consulta do CEP {Cep}: HTTP {Status}.",
                    cepDigitos, (int)resposta.StatusCode);
                return null;
            }

            var corpo = await resposta.Content.ReadFromJsonAsync<RespostaGoogleGeocoding>(ct);

            // REQUEST_DENIED é chave inválida, API Geocoding não habilitada no
            // projeto do Google Cloud, ou faturamento não ativado — três causas
            // diferentes que o log precisa distinguir de "não achou".
            if (corpo?.Status is not "OK")
            {
                _logger.LogInformation(
                    "Google Geocoding retornou {Status} para o CEP {Cep}.",
                    corpo?.Status ?? "resposta vazia", cepDigitos);
                return null;
            }

            var geometria = corpo.Results?.FirstOrDefault()?.Geometry;
            if (geometria?.Location is null)
                return null;

            var aproximada = geometria.LocationType is "APPROXIMATE" or "GEOMETRIC_CENTER";

            return (geometria.Location.Lat, geometria.Location.Lng, aproximada);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao chamar o Google Geocoding para o CEP {Cep}.", cepDigitos);
            return null;
        }
    }

    // Indexa CEP, não logradouro do OSM, então responde onde o Nominatim não
    // responde. O preço é a precisão: medido contra um endereço conhecido, errou
    // ~1,8 km — daí a coordenada voltar marcada como aproximada, para a tela
    // pedir conferência em vez de fingir exatidão.
    private async Task<(double Latitude, double Longitude)?> CoordenadaPorCepAsync(
        string cepDigitos, CancellationToken ct)
    {
        try
        {
            using var resposta = await _http.GetAsync(
                $"https://cep.awesomeapi.com.br/json/{cepDigitos}", ct);

            if (!resposta.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Coordenada por CEP {Cep} recusada: HTTP {Status}.",
                    cepDigitos, (int)resposta.StatusCode);
                return null;
            }

            var achado = await resposta.Content.ReadFromJsonAsync<RespostaCepComCoordenada>(ct);

            if (achado is null
                || !double.TryParse(achado.Lat, System.Globalization.CultureInfo.InvariantCulture, out var lat)
                || !double.TryParse(achado.Lng, System.Globalization.CultureInfo.InvariantCulture, out var lng))
            {
                _logger.LogInformation("CEP {Cep} sem coordenada na rede de segurança.", cepDigitos);
                return null;
            }

            _logger.LogInformation(
                "CEP {Cep} resolvido por coordenada aproximada de CEP.", cepDigitos);

            return (lat, lng);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao buscar coordenada do CEP {Cep}.", cepDigitos);
            return null;
        }
    }

    private async Task<(double Latitude, double Longitude)?> BuscarCoordenadaAsync(
        string parametros, string? cep, CancellationToken ct)
    {
        try
        {
            using var resposta = await _http.GetAsync(
                $"https://nominatim.openstreetmap.org/search?format=json&limit=1&{parametros}", ct);

            // Status explícito no log: 403 é User-Agent recusado e 429 é limite
            // de uso estourado — problemas diferentes, correções diferentes.
            // Sem isso os dois apareciam como "falha ao geocodificar".
            if (!resposta.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Nominatim recusou a consulta do CEP {Cep}: HTTP {Status}.",
                    cep, (int)resposta.StatusCode);
                return null;
            }

            var achados = await resposta.Content
                .ReadFromJsonAsync<IReadOnlyList<RespostaNominatim>>(ct);

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

    private sealed record RespostaCepComCoordenada(
        [property: JsonPropertyName("lat")] string? Lat,
        [property: JsonPropertyName("lng")] string? Lng);

    private sealed record RespostaNominatim(
        [property: JsonPropertyName("lat")] string? Lat,
        [property: JsonPropertyName("lon")] string? Lon);

    private sealed record RespostaGoogleGeocoding(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("results")] IReadOnlyList<GoogleResultado>? Results);

    private sealed record GoogleResultado(
        [property: JsonPropertyName("geometry")] GoogleGeometria? Geometry);

    private sealed record GoogleGeometria(
        [property: JsonPropertyName("location")] GoogleLocation? Location,
        [property: JsonPropertyName("location_type")] string? LocationType);

    private sealed record GoogleLocation(
        [property: JsonPropertyName("lat")] double Lat,
        [property: JsonPropertyName("lng")] double Lng);
}
