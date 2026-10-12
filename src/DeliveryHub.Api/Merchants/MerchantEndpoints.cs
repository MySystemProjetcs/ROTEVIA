using System.Text.Json;
using System.Numerics;
using DeliveryHub.Application.Abstractions;
using DeliveryHub.Application.Merchants;
using DeliveryHub.Domain.SharedKernel;
using DeliveryHub.Infrastructure.Integrations.DiDiFood.Auth;
using Microsoft.AspNetCore.Http.HttpResults;
// Alias: dentro de DeliveryHub.Api.Merchants o nome "Merchants" já está
// ocupado pelo próprio namespace, então o tipo do domínio precisa de apelido
// para não ficar ambíguo.
using MerchantEntity = DeliveryHub.Domain.Merchants.Merchant;

namespace DeliveryHub.Api.Merchants;

public sealed record IniciarConexaoResponse(string UserCode, string VerificationUrlComplete, DateTimeOffset ExpiraEm);
public sealed record ConfirmarConexaoRequest(string AuthorizationCode);
public sealed record DefinirTaxaRequest(decimal Valor);
public sealed record DefinirNomeRequest(string Nome);
public sealed record VincularNoventaENoveRequest(string AppShopId, string? AppId99 = null);
public sealed record TesteAutenticacao99Response(
    bool Sucesso, string? AppId99, string? AppShopId, string? TokenMascarado, string? Erro);

public sealed record Autorizacao99Response(string Url);

// Status de uma plataforma de origem de pedido, normalizado pela Api: o
// componente do painel não precisa conhecer o formato de cada marketplace.
// Disponibilidade é string e não enum porque "sem-vinculo" não é um estado
// da loja na plataforma — é a ausência de integração, e o front renderiza
// os quatro casos de forma diferente.
public sealed record StatusPlataforma(
    bool Vinculado,
    bool Autenticado,
    string? LojaNome,
    string Disponibilidade,
    DateTimeOffset? AtualizadoEm,
    string? Erro);

public sealed record StatusIntegracoesResponse(
    StatusPlataforma IFood,
    StatusPlataforma NoventaENove);

public sealed record GetUrlAutorizacaoRequest(BigInteger AppId);
public sealed record TaxaEntregaResponse(decimal Valor);
public sealed record ConfigurarHorarioFuncionamentoRequest(IReadOnlyList<IFoodOpeningHourShift> Shifts);
public sealed record ContextoIFoodResponse(
    JsonElement Comerciante,
    JsonElement Status,
    JsonElement HorarioFuncionamento,
    DateTimeOffset AtualizadoEm,
    bool Desatualizado);

// Latitude/longitude opcionais: só são necessárias quando o geocoder não
// encontra o logradouro, o que acontece de verdade em rua fora do OpenStreetMap.
public sealed record EnderecoDaLojaRequest(
    string Cep,
    string Numero,
    string? Complemento,
    string? Referencia,
    double? Latitude,
    double? Longitude);

public static class MerchantEndpoints
{
    public static void MapMerchantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/restaurantes")
            .WithTags("Restaurantes")
            .RequireAuthorization(Identity.Policies.OperadorDaLoja);

        group.MapPost("/{merchantId:guid}/ifood/iniciar-conexao", IniciarConexao);
        group.MapPost("/{merchantId:guid}/ifood/confirmar", ConfirmarConexao);
        group.MapGet("/ifood/contexto", ObterContextoIFood);
        group.MapGet("/ifood/horario-funcionamento", ObterHorarioFuncionamentoIFood);
        group.MapPut("/ifood/horario-funcionamento", ConfigurarHorarioFuncionamentoIFood);
        group.MapPut("/{merchantId:guid}/nome", DefinirNome);
        group.MapPut("/{merchantId:guid}/taxa-entrega", DefinirTaxa);
        group.MapGet("/{merchantId:guid}/endereco", ObterEndereco);
        group.MapPut("/{merchantId:guid}/endereco", DefinirEndereco);
        group.MapPut("/{merchantId:guid}/99food/app-shop-id", VincularNoventaENove);
        group.MapPost("/{merchantId:guid}/99food/testar-autenticacao", TestarAutenticacao99Food);
        group.MapPost("/{merchantId:guid}/99food/autorizacao/get-url", GetUrlAutorizacao99);
        group.MapPost("/{merchantId:guid}/99food/autorizacao", GetUrlAutorizacao99);
        group.MapGet("/{merchantId:guid}/integracoes/status", ObterStatusIntegracoes);
    }

    // Status consolidado das origens de pedido. Uma chamada só porque o painel
    // mostra as duas lado a lado — dois endpoints fariam o componente esperar
    // o mais lento dos dois de qualquer forma, e ainda duplicariam o carregar
    // do Merchant.
    private static async Task<Results<Ok<StatusIntegracoesResponse>, ProblemHttpResult>> ObterStatusIntegracoes(
        Guid merchantId,
        ITenantContext tenant,
        IMerchantRepository merchants,
        IObterContextoIFood contextoIFood,
        IDiDiFoodAuthenticator autenticar99,
        TimeProvider relogio,
        CancellationToken ct)
    {
        if (!PodeAcessar(tenant, merchantId))
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        var merchant = await merchants.ObterPorIdAsync(merchantId, ct);
        if (merchant is null)
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        // As duas consultas são independentes e falham de forma isolada: uma
        // plataforma fora do ar não pode esconder o status da outra. Em
        // paralelo porque o painel espera o resultado das duas para pintar.
        var tarefaIFood = StatusDoIFoodAsync(merchant.Id, merchant, contextoIFood, ct);
        var tarefa99 = StatusDaNoventaENoveAsync(merchant, autenticar99, relogio, ct);
        await Task.WhenAll(tarefaIFood, tarefa99);

        return TypedResults.Ok(new StatusIntegracoesResponse(
            await tarefaIFood,
            await tarefa99));
    }

    private static async Task<StatusPlataforma> StatusDoIFoodAsync(
        Guid merchantId,
        MerchantEntity merchant,
        IObterContextoIFood obter,
        CancellationToken ct)
    {
        if (merchant.IFoodMerchantId is null || merchant.ConexaoIFood?.Conectado != true)
            return new StatusPlataforma(false, false, null, "sem-vinculo", null, null);

        var resultado = await obter.ExecutarAsync(merchantId, ct);
        if (resultado.IsFailure)
            return new StatusPlataforma(true, false, null, "indisponivel", null, resultado.Error.Message);

        var contexto = resultado.Value;
        return new StatusPlataforma(
            Vinculado: true,
            Autenticado: true,
            LojaNome: NomeDaLojaIFood(contexto.DetailsJson),
            Disponibilidade: DisponibilidadeDoIFood(contexto.StatusJson),
            AtualizadoEm: contexto.UpdatedAt,
            Erro: contexto.IsStale ? "Dados podem estar desatualizados." : null);
    }

    // Fase 1: autenticar com sucesso é o sinal de que a loja está acessível.
    // O nome e a disponibilidade reais exigem GET /v1/shop/shop/detail, que
    // só podemos chamar depois de ter o auth_token — fica para a fase 2,
    // quando houver loja vinculada em conta com acesso.
    private static async Task<StatusPlataforma> StatusDaNoventaENoveAsync(
        MerchantEntity merchant,
        IDiDiFoodAuthenticator autenticar,
        TimeProvider relogio,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(merchant.NoventaENoveAppShopId))
            return new StatusPlataforma(false, false, null, "sem-vinculo", null, null);

        try
        {
            await autenticar.GetAuthTokenAsync(merchant.NoventaENoveAppShopId, merchant.NoventaENoveAppId, ct);
            return new StatusPlataforma(true, true, null, "online", relogio.GetUtcNow(), null);
        }
        // A 99Food devolve HTTP 200 com errno != 0 para erro de negócio, que o
        // autenticador converte em InvalidOperationException com o errmsg
        // original — é essa mensagem que diagnostica (errno=10101 loja não
        // vinculada, errno=14106 app_id/app_secret de apps diferentes).
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            return new StatusPlataforma(true, false, null, "indisponivel", relogio.GetUtcNow(), ex.Message);
        }
    }

    private static string? NomeDaLojaIFood(string detailsJson)
    {
        using var documento = JsonDocument.Parse(detailsJson);
        return documento.RootElement.TryGetProperty("name", out var nome) && nome.ValueKind == JsonValueKind.String
            ? nome.GetString()
            : null;
    }

    // O iFood devolve um array de operações; a que interessa ao painel é a de
    // entrega no canal do próprio iFood. Quando há uma só operação de entrega,
    // ela vale mesmo sem o canal bater — loja com um canal só não precisa
    // declarar qual é.
    private static string DisponibilidadeDoIFood(string statusJson)
    {
        using var documento = JsonDocument.Parse(statusJson);
        if (documento.RootElement.ValueKind != JsonValueKind.Array)
            return "indisponivel";

        var entregas = documento.RootElement
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty("operation", out var operacao)
                && operacao.ValueKind == JsonValueKind.String
                && string.Equals(operacao.GetString(), "DELIVERY", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (entregas.Count == 0)
            return "indisponivel";

        var doCanalIFood = entregas.FirstOrDefault(item =>
            item.TryGetProperty("salesChannel", out var canal)
            && canal.ValueKind == JsonValueKind.String
            && string.Equals(canal.GetString(), "IFOOD", StringComparison.OrdinalIgnoreCase));

        var escolhida = doCanalIFood.ValueKind == JsonValueKind.Object
            ? doCanalIFood
            : entregas.Count == 1 ? entregas[0] : default;

        if (escolhida.ValueKind != JsonValueKind.Object
            || !escolhida.TryGetProperty("available", out var disponivel)
            || disponivel.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            return "indisponivel";

        return disponivel.GetBoolean() ? "online" : "offline";
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> VincularNoventaENove(
        Guid merchantId,
        VincularNoventaENoveRequest request,
        ITenantContext tenant,
        IVincularLojaNoventaENove vincular,
        CancellationToken ct)
    {
        if (!PodeAcessar(tenant, merchantId))
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        var resultado = await vincular.ExecutarAsync(merchantId, request.AppShopId, request.AppId99, ct);
        return resultado.IsSuccess ? TypedResults.NoContent() : ProblemaDe(resultado.Error);
    }

    // Diagnóstico: roda a autenticação da 99Food com as credenciais da loja e
    // devolve o resultado para inspeção. O token sai mascarado — o valor
    // completo nunca cruza a fronteira da Api. Sem isto, saber se app_id e
    // app_shop_id estão corretos exigia disparar um pedido real.
    private static async Task<Results<Ok<TesteAutenticacao99Response>, ProblemHttpResult>> TestarAutenticacao99Food(
        Guid merchantId,
        ITenantContext tenant,
        IMerchantRepository merchants,
        IDiDiFoodAuthenticator autenticar,
        CancellationToken ct)
    {
        if (!PodeAcessar(tenant, merchantId))
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        var merchant = await merchants.ObterPorIdAsync(merchantId, ct);
        if (merchant is null)
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        if (string.IsNullOrWhiteSpace(merchant.NoventaENoveAppShopId))
            return TypedResults.Ok(new TesteAutenticacao99Response(
                false, merchant.NoventaENoveAppId, null, null, "Loja sem app_shop_id vinculado."));

        try
        {
            var token = await autenticar.GetAuthTokenAsync(
                merchant.NoventaENoveAppShopId, merchant.NoventaENoveAppId, ct);

            var mascara = token.Length <= 8 ? "****" : $"{token[..4]}…{token[^4..]}";

            return TypedResults.Ok(new TesteAutenticacao99Response(
                true, merchant.NoventaENoveAppId, merchant.NoventaENoveAppShopId, mascara, null));
        }
        catch (HttpRequestException ex)
        {
            return TypedResults.Ok(new TesteAutenticacao99Response(
                false, merchant.NoventaENoveAppId, merchant.NoventaENoveAppShopId, null, ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Ok(new TesteAutenticacao99Response(
                false, merchant.NoventaENoveAppId, merchant.NoventaENoveAppShopId, null, ex.Message));
        }
    }

    private static async Task<Results<Ok<Autorizacao99Response>, ProblemHttpResult>> GetUrlAutorizacao99(
        Guid merchantId,
        ITenantContext tenant,
        IMerchantRepository merchants,
        IDiDiFoodAuthenticator autenticar,
        CancellationToken ct)
    {
        if (!PodeAcessar(tenant, merchantId))
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        var merchant = await merchants.ObterPorIdAsync(merchantId, ct);
        if (merchant is null)
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        if (string.IsNullOrWhiteSpace(merchant.NoventaENoveAppShopId))
            return TypedResults.Ok(new Autorizacao99Response(
                $"https://didi-food.com/en-US/store/ui-sdk/authorization?appId={merchant.NoventaENoveAppId}&appShopId={merchant.NoventaENoveAppShopId}"));

        try
        {
            var token = await autenticar.GetAuthTokenAsync(
                merchant.NoventaENoveAppShopId, merchant.NoventaENoveAppId, ct);

            var mascara = token.Length <= 8 ? "****" : $"{token[..4]}…{token[^4..]}";

            return TypedResults.Ok(new Autorizacao99Response(
                $"https://didi-food.com/en-US/store/ui-sdk/authorization?appId={merchant.NoventaENoveAppId}&appShopId={merchant.NoventaENoveAppShopId}&token={mascara}"));
        }
        catch (HttpRequestException)
        {
            return TypedResults.Ok(new Autorizacao99Response(
                $"https://didi-food.com/en-US/store/ui-sdk/authorization?appId={merchant.NoventaENoveAppId}&appShopId={merchant.NoventaENoveAppShopId}"));
        }
        catch (InvalidOperationException)
        {
            return TypedResults.Ok(new Autorizacao99Response(
                $"https://didi-food.com/en-US/store/ui-sdk/authorization?appId={merchant.NoventaENoveAppId}&appShopId={merchant.NoventaENoveAppShopId}"));
        }
    }

    private static async Task<Results<Ok<JsonElement>, ProblemHttpResult>> GetUrlAutorizacaoExterno(
        BigInteger appId,
        CancellationToken ct,
        IHttpClientFactory httpClientFactory)
    {
        var client = httpClientFactory.CreateClient("DiDiFoodAuthenticator");
        var response = await client.PostAsync("/v1/auth/authorizationpage/getUrl", new StringContent(
            $"{{\"app_id\": {appId}}}", System.Text.Encoding.UTF8, "application/json"));

        if (!response.IsSuccessStatusCode)
        {
            return TypedResults.Ok(JsonDocument.Parse("{}").RootElement.Clone());
        }

        var content = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(content);
        return TypedResults.Ok(doc.RootElement.Clone());
    }

    private static async Task<Results<Ok<ContextoIFoodResponse>, ProblemHttpResult>> ObterContextoIFood(
        ITenantContext tenant,
        IObterContextoIFood obter,
        CancellationToken ct)
    {
        if (tenant.MerchantId is not Guid merchantId)
            return TypedResults.Problem(
                title: "A sessão não está vinculada a um restaurante.",
                detail: "ifood.merchant_contexto_indisponivel",
                statusCode: StatusCodes.Status400BadRequest);

        var resultado = await obter.ExecutarAsync(merchantId, ct);
        if (resultado.IsFailure)
        {
            var statusCode = resultado.Error.Type switch
            {
                _ when resultado.Error.Code == ConexaoIFoodErrors.AutenticacaoIntegradaFalhou.Code =>
                    StatusCodes.Status502BadGateway,
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorType.Failure => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status500InternalServerError
            };

            return TypedResults.Problem(
                title: resultado.Error.Message,
                detail: resultado.Error.Code,
                statusCode: statusCode);
        }

        var contexto = resultado.Value;
        return TypedResults.Ok(new ContextoIFoodResponse(
            JsonDocument.Parse(contexto.DetailsJson).RootElement.Clone(),
            JsonDocument.Parse(contexto.StatusJson).RootElement.Clone(),
            JsonDocument.Parse(contexto.OpeningHoursJson).RootElement.Clone(),
            contexto.UpdatedAt,
            contexto.IsStale));
    }

    private static async Task<Results<Ok<JsonElement>, NotFound, ProblemHttpResult>> ObterHorarioFuncionamentoIFood(
        ITenantContext tenant,
        IObterHorarioFuncionamentoIFood obter,
        CancellationToken ct)
    {
        if (tenant.MerchantId is not Guid merchantId)
            return TypedResults.Problem(
                title: "A sessão não está vinculada a um restaurante.",
                detail: "ifood.merchant_contexto_indisponivel",
                statusCode: StatusCodes.Status400BadRequest);

        var resultado = await obter.ExecutarAsync(merchantId, ct);
        if (resultado.IsFailure)
            return ProblemaDe(resultado.Error);

        using var documento = JsonDocument.Parse(resultado.Value);
        var horarios = documento.RootElement.Clone();
        if (horarios.ValueKind == JsonValueKind.Array && horarios.GetArrayLength() == 0)
            return TypedResults.NotFound();

        return TypedResults.Ok(horarios);
    }

    private static async Task<Results<Created<JsonElement>, ProblemHttpResult>> ConfigurarHorarioFuncionamentoIFood(
        ConfigurarHorarioFuncionamentoRequest request,
        ITenantContext tenant,
        IConfigurarHorarioFuncionamentoIFood configurar,
        CancellationToken ct)
    {
        if (tenant.MerchantId is not Guid merchantId)
            return TypedResults.Problem(
                title: "A sessão não está vinculada a um restaurante.",
                detail: "ifood.merchant_contexto_indisponivel",
                statusCode: StatusCodes.Status400BadRequest);

        var resultado = await configurar.ExecutarAsync(
            merchantId,
            new IFoodOpeningHoursInput(request.Shifts),
            ct);

        if (resultado.IsFailure)
            return ProblemaDe(resultado.Error);

        using var documento = JsonDocument.Parse(resultado.Value);
        return TypedResults.Created((string?)null, documento.RootElement.Clone());
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DefinirNome(
        Guid merchantId,
        DefinirNomeRequest request,
        ITenantContext tenant,
        IDefinirNomeDaLoja definir,
        CancellationToken ct)
    {
        if (!PodeAcessar(tenant, merchantId))
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        var resultado = await definir.ExecutarAsync(merchantId, request.Nome, ct);

        return resultado.IsSuccess ? TypedResults.NoContent() : ProblemaDe(resultado.Error);
    }

    private static async Task<Results<Ok<EnderecoDaLojaDto>, NotFound, ProblemHttpResult>> ObterEndereco(
        Guid merchantId,
        ITenantContext tenant,
        IObterEnderecoDaLoja obter,
        CancellationToken ct)
    {
        if (!PodeAcessar(tenant, merchantId))
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        var endereco = await obter.ExecutarAsync(merchantId, ct);

        return endereco is null ? TypedResults.NotFound() : TypedResults.Ok(endereco);
    }

    private static async Task<Results<Ok<EnderecoDaLojaDto>, ProblemHttpResult>> DefinirEndereco(
        Guid merchantId,
        EnderecoDaLojaRequest request,
        ITenantContext tenant,
        IDefinirEnderecoDaLoja definir,
        CancellationToken ct)
    {
        if (!PodeAcessar(tenant, merchantId))
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        var resultado = await definir.ExecutarAsync(
            merchantId,
            new NovoEnderecoDaLoja(
                request.Cep,
                request.Numero,
                request.Complemento,
                request.Referencia,
                request.Latitude,
                request.Longitude),
            ct);

        return resultado.IsSuccess ? TypedResults.Ok(resultado.Value) : ProblemaDe(resultado.Error);
    }

    private static async Task<Results<Ok<IniciarConexaoResponse>, ProblemHttpResult>> IniciarConexao(
        Guid merchantId,
        ITenantContext tenant,
        IIniciarConexaoIFood iniciar,
        CancellationToken ct)
    {
        if (!PodeAcessar(tenant, merchantId))
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        var resultado = await iniciar.ExecutarAsync(merchantId, ct);

        return resultado.IsSuccess
            ? TypedResults.Ok(new IniciarConexaoResponse(
                resultado.Value.UserCode, resultado.Value.VerificationUrlComplete, resultado.Value.ExpiraEm))
            : ProblemaDe(resultado.Error);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ConfirmarConexao(
        Guid merchantId,
        ConfirmarConexaoRequest request,
        ITenantContext tenant,
        IConfirmarConexaoIFood confirmar,
        CancellationToken ct)
    {
        if (!PodeAcessar(tenant, merchantId))
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        var resultado = await confirmar.ExecutarAsync(merchantId, request.AuthorizationCode, ct);

        return resultado.IsSuccess ? TypedResults.NoContent() : ProblemaDe(resultado.Error);
    }

    private static async Task<Results<Ok<TaxaEntregaResponse>, ProblemHttpResult>> DefinirTaxa(
        Guid merchantId,
        DefinirTaxaRequest request,
        ITenantContext tenant,
        IDefinirTaxaPorEntrega definir,
        CancellationToken ct)
    {
        if (!PodeAcessar(tenant, merchantId))
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        var resultado = await definir.ExecutarAsync(merchantId, request.Valor, ct);

        return resultado.IsSuccess
            ? TypedResults.Ok(new TaxaEntregaResponse(resultado.Value))
            : ProblemaDe(resultado.Error);
    }

    // A policy só garante que existe ALGUM merchant_id no token; sem isto, o
    // dono da Loja A poderia iniciar/confirmar a conexão da Loja B só trocando
    // o GUID na URL. 404 em vez de 403 — não revela que a loja alheia existe.
    private static bool PodeAcessar(ITenantContext tenant, Guid merchantId) =>
        tenant.PodeVerTodosOsTenants || tenant.MerchantId == merchantId;

    private static ProblemHttpResult ProblemaDe(Error erro) => TypedResults.Problem(
        title: erro.Message,
        detail: erro.Code,
        statusCode: erro.Code == ConexaoIFoodErrors.AutenticacaoIntegradaFalhou.Code
            ? StatusCodes.Status502BadGateway
            : erro.Type switch
            {
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorType.Failure => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status500InternalServerError
            });
}
