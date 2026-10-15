using System.Text.Json;
using System.Numerics;
using DeliveryHub.Application.Abstractions;
using DeliveryHub.Application.Merchants;
using DeliveryHub.Domain.SharedKernel;
using DeliveryHub.Infrastructure.Integrations.DiDiFood.Auth;
using DeliveryHub.Infrastructure.Integrations.DiDiFood.Catalog;
using DeliveryHub.Infrastructure.Integrations.IFood;
using DeliveryHub.Infrastructure.Integrations.IFood.Catalog;
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

public sealed record ItemCardapioResponse(
    string AppItemId,
    string Nome,
    string? Descricao,
    decimal Preco,
    bool Disponivel,
    bool VendidoSeparadamente,
    string? ImagemUrl);

public sealed record Categoria99Response(
    string AppCategoryId,
    string Nome,
    IReadOnlyList<ItemCardapioResponse> Itens);

public sealed record Cardapio99Response(
    int TotalItens,
    IReadOnlyList<Categoria99Response> Categorias,
    IReadOnlyList<ItemCardapioResponse> ItensSemCategoria);

public sealed record DefinirDisponibilidadeRequest(
    IReadOnlyList<string> AppItemIds,
    bool Disponivel);

public sealed record DisponibilidadeResponse(
    IReadOnlyList<string> Sucesso,
    IReadOnlyList<string> Falha);

public sealed record CatalogoIFoodResumo(
    string CatalogId,
    string? Status,
    IReadOnlyList<string> Context,
    DateTimeOffset? ModifiedAt);

public sealed record StatusCatalogoIFoodResponse(
    bool EscopoLiberado,
    int Quantidade,
    IReadOnlyList<CatalogoIFoodResumo> Catalogos);

public sealed record ItemCatalogoIFoodResponse(
    string ItemId,
    string Nome,
    string? Descricao,
    decimal Preco,
    decimal? PrecoOriginal,
    bool Disponivel,
    IReadOnlyList<string> Restricoes,
    string? ImagemUrl);

public sealed record CategoriaCatalogoIFoodResponse(
    string CategoryId,
    string Nome,
    bool Pausada,
    IReadOnlyList<string> Restricoes,
    IReadOnlyList<ItemCatalogoIFoodResponse> Itens);

public sealed record CatalogoCompletoIFoodResponse(
    string CatalogId,
    string GroupId,
    int TotalItens,
    IReadOnlyList<CategoriaCatalogoIFoodResponse> Categorias);

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
        group.MapGet("/{merchantId:guid}/catalogo/status", ObterStatusCatalogoIFood);
        group.MapGet("/{merchantId:guid}/catalogo/ifood", ObterCatalogoCompletoIFood);
        group.MapGet("/{merchantId:guid}/catalogo/99food", ObterCardapio99Food);
        group.MapPut("/{merchantId:guid}/catalogo/99food/disponibilidade", DefinirDisponibilidade99Food);
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
        var tarefaIFood = StatusDoIFoodAsync(merchant, contextoIFood, relogio, ct);
        var tarefa99 = StatusDaNoventaENoveAsync(merchant, autenticar99, relogio, ct);
        await Task.WhenAll(tarefaIFood, tarefa99);

        return TypedResults.Ok(new StatusIntegracoesResponse(
            await tarefaIFood,
            await tarefa99));
    }

    // O try/catch aqui não contraria a regra do CLAUDE.md §8 (try/catch por
    // endpoint é proibido): não estamos engolindo falha do endpoint, estamos
    // convertendo o estado de uma integração externa em dado de resposta. Uma
    // exceção escapando daqui derrubaria o status da OUTRA plataforma junto,
    // que é exatamente o que este endpoint existe para evitar.
    private static async Task<StatusPlataforma> StatusDoIFoodAsync(
        MerchantEntity merchant,
        IObterContextoIFood obter,
        TimeProvider relogio,
        CancellationToken ct)
    {
        if (merchant.IFoodMerchantId is null || merchant.ConexaoIFood?.Conectado != true)
            return new StatusPlataforma(false, false, null, "sem-vinculo", null, null);

        try
        {
            var resultado = await obter.ExecutarAsync(merchant.Id, ct);
            if (resultado.IsFailure)
                return new StatusPlataforma(
                    true, false, null, "indisponivel", relogio.GetUtcNow(), resultado.Error.Message);

            var contexto = resultado.Value;
            return new StatusPlataforma(
                Vinculado: true,
                Autenticado: true,
                LojaNome: StatusDaLojaIFood.Nome(contexto.DetailsJson),
                Disponibilidade: StatusDaLojaIFood.Disponibilidade(contexto.StatusJson),
                AtualizadoEm: contexto.UpdatedAt,
                Erro: contexto.IsStale ? "Dados podem estar desatualizados." : null);
        }
        catch (Exception ex) when (FalhaDeIntegracao(ex, ct))
        {
            return new StatusPlataforma(true, false, null, "indisponivel", relogio.GetUtcNow(), MotivoDe(ex));
        }
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
        catch (Exception ex) when (FalhaDeIntegracao(ex, ct))
        {
            return new StatusPlataforma(true, false, null, "indisponivel", relogio.GetUtcNow(), MotivoDe(ex));
        }
    }

    // Timeout do HttpClient chega como OperationCanceledException sem que o
    // token do request tenha sido cancelado — é falha da plataforma, vira
    // status. Já um cancelamento real (navegador fechou a aba) tem que subir:
    // não há ninguém para ler a resposta, e engolir mascararia o motivo.
    private static bool FalhaDeIntegracao(Exception ex, CancellationToken ct) => ex switch
    {
        OperationCanceledException => !ct.IsCancellationRequested,
        HttpRequestException or InvalidOperationException or JsonException => true,
        _ => false,
    };

    // A 99Food devolve HTTP 200 com errno != 0 para erro de negócio, e o
    // autenticador converte isso em InvalidOperationException carregando o
    // errmsg original — é o texto que diagnostica (errno=10101 loja não
    // vinculada ao app, errno=14106 app_id e app_secret de apps diferentes),
    // e o que o lojista repassa ao suporte.
    private static string MotivoDe(Exception ex) => ex is OperationCanceledException
        ? "Tempo esgotado ao consultar a plataforma."
        : ex.Message;


    // Responde a pergunta que bloqueia o módulo de catálogo: o escopo está
    // liberado para este clientId no iFood? O app do projeto nasceu com
    // Order, Events e Merchant (CLAUDE.md §7); Catalog precisa ser habilitado
    // no portal, e até lá a API recusa com 403. A aba de Catálogo do painel
    // chama isto para o lojista descobrir sozinho quando foi liberado.
    private static async Task<Results<Ok<StatusCatalogoIFoodResponse>, ProblemHttpResult>> ObterStatusCatalogoIFood(
        Guid merchantId,
        ITenantContext tenant,
        IMerchantRepository merchants,
        IIFoodCatalogClient catalogo,
        CancellationToken ct)
    {
        if (!PodeAcessar(tenant, merchantId))
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        var merchant = await merchants.ObterPorIdAsync(merchantId, ct);
        if (merchant is null)
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        if (merchant.IFoodMerchantId is not Guid ifoodMerchantId)
            return TypedResults.Problem(
                title: "Restaurante sem vínculo com o iFood.",
                detail: "merchant.sem_vinculo_ifood",
                statusCode: StatusCodes.Status409Conflict);

        try
        {
            var catalogos = await catalogo.ListarCatalogosAsync(ifoodMerchantId, ct);

            return TypedResults.Ok(new StatusCatalogoIFoodResponse(
                EscopoLiberado: true,
                Quantidade: catalogos.Count,
                Catalogos: catalogos
                    .Select(c => new CatalogoIFoodResumo(c.CatalogId, c.Status, c.Context, c.ModifiedAt))
                    .ToList()));
        }
        catch (IFoodApiException ex)
        {
            // 403 aqui quase sempre é módulo não habilitado, não credencial
            // ruim — o mesmo token funciona em Order e Events. A distinção
            // importa porque a ação do lojista é outra em cada caso.
            return TypedResults.Problem(
                title: ex.StatusCode == System.Net.HttpStatusCode.Forbidden
                    ? "O módulo de catálogo ainda não está habilitado para este aplicativo no iFood."
                    : "Não foi possível consultar o catálogo no iFood.",
                detail: ex.Message,
                statusCode: (int)ex.StatusCode);
        }
    }


    // Catálogo completo do iFood: categorias e itens vendáveis, com restrições
    // apontando o que está pausado ou com violação. Resolve o groupId sozinho
    // (primeiro catálogo DEFAULT) pra o painel não precisar passar nada além
    // do merchantId. Se a loja tiver só catálogo INDOOR, cai pro primeiro —
    // é raro e documentar seria pior UX do que mostrar o que existe.
    private static async Task<Results<Ok<CatalogoCompletoIFoodResponse>, ProblemHttpResult>> ObterCatalogoCompletoIFood(
        Guid merchantId,
        ITenantContext tenant,
        IMerchantRepository merchants,
        IIFoodCatalogClient catalogo,
        CancellationToken ct)
    {
        if (!PodeAcessar(tenant, merchantId))
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        var merchant = await merchants.ObterPorIdAsync(merchantId, ct);
        if (merchant is null)
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        if (merchant.IFoodMerchantId is not Guid ifoodMerchantId)
            return TypedResults.Problem(
                title: "Restaurante sem vínculo com o iFood.",
                detail: "merchant.sem_vinculo_ifood",
                statusCode: StatusCodes.Status409Conflict);

        try
        {
            var catalogos = await catalogo.ListarCatalogosAsync(ifoodMerchantId, ct);

            // DEFAULT (delivery) sempre primeiro. Se não houver, cai pro primeiro
            // disponível — INDOOR é melhor que erro de "sem catálogo", pois a
            // loja consegue ao menos ver o cardápio de consumo no local.
            var catalogoAlvo = catalogos.FirstOrDefault(c => c.Context.Contains("DEFAULT"))
                ?? catalogos.FirstOrDefault();

            if (catalogoAlvo is null)
                return TypedResults.Problem(
                    title: "A loja não tem catálogo publicado no iFood.",
                    detail: "catalogo.ifood.sem_catalogo",
                    statusCode: StatusCodes.Status404NotFound);

            if (string.IsNullOrWhiteSpace(catalogoAlvo.GroupId))
                return TypedResults.Problem(
                    title: "O catálogo da loja no iFood está sem groupId.",
                    detail: "catalogo.ifood.sem_group_id",
                    statusCode: StatusCodes.Status409Conflict);

            var completo = await catalogo.ObterCatalogoCompletoAsync(
                ifoodMerchantId, catalogoAlvo.CatalogId, catalogoAlvo.GroupId, ct);

            return TypedResults.Ok(new CatalogoCompletoIFoodResponse(
                CatalogId: catalogoAlvo.CatalogId,
                GroupId: completo.GroupId,
                TotalItens: completo.TotalItens,
                Categorias: completo.Categorias
                    .Select(c => new CategoriaCatalogoIFoodResponse(
                        c.CategoryId,
                        c.Nome,
                        c.Pausada,
                        c.Restricoes,
                        c.Itens.Select(i => new ItemCatalogoIFoodResponse(
                            i.ItemId, i.Nome, i.Descricao,
                            i.Preco, i.PrecoOriginal,
                            i.Disponivel, i.Restricoes, i.ImagemUrl)).ToList()))
                    .ToList()));
        }
        catch (IFoodApiException ex)
        {
            // 403 em sellableItems também significa escopo não liberado — mesma
            // mensagem do /catalogo/status, pra o painel não precisar diferenciar.
            return TypedResults.Problem(
                title: ex.StatusCode == System.Net.HttpStatusCode.Forbidden
                    ? "O módulo de catálogo ainda não está habilitado para este aplicativo no iFood."
                    : "Não foi possível carregar o catálogo do iFood.",
                detail: ex.Message,
                statusCode: (int)ex.StatusCode);
        }
    }

    // Cardápio publicado na 99Food. Diferente do iFood, aqui não há questão de
    // escopo: o acesso vem do auth_token da loja, então a falha possível é a
    // loja não estar autorizada ao app (errno 10101).
    private static async Task<Results<Ok<Cardapio99Response>, ProblemHttpResult>> ObterCardapio99Food(
        Guid merchantId,
        ITenantContext tenant,
        IMerchantRepository merchants,
        IDiDiFoodCatalogClient catalogo,
        CancellationToken ct)
    {
        if (!PodeAcessar(tenant, merchantId))
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        var merchant = await merchants.ObterPorIdAsync(merchantId, ct);
        if (merchant is null)
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        if (string.IsNullOrWhiteSpace(merchant.NoventaENoveAppShopId))
            return TypedResults.Problem(
                title: "Restaurante sem vínculo com a 99Food.",
                detail: "merchant.sem_vinculo_99food",
                statusCode: StatusCodes.Status409Conflict);

        try
        {
            var cardapio = await catalogo.ObterCardapioAsync(
                merchant.NoventaENoveAppShopId, merchant.NoventaENoveAppId, ct);

            return TypedResults.Ok(new Cardapio99Response(
                cardapio.TotalItens,
                cardapio.Categorias
                    .Select(c => new Categoria99Response(
                        c.AppCategoryId,
                        c.Nome,
                        c.Itens.Select(ParaResponse).ToList()))
                    .ToList(),
                cardapio.ItensSemCategoria.Select(ParaResponse).ToList()));
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            // A mensagem carrega o errno/errmsg da 99 — é o que diagnostica e
            // o que o lojista repassa ao suporte deles.
            return TypedResults.Problem(
                title: "Não foi possível consultar o cardápio na 99Food.",
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task<Results<Ok<DisponibilidadeResponse>, ProblemHttpResult>> DefinirDisponibilidade99Food(
        Guid merchantId,
        DefinirDisponibilidadeRequest request,
        ITenantContext tenant,
        IMerchantRepository merchants,
        IDiDiFoodCatalogClient catalogo,
        CancellationToken ct)
    {
        if (!PodeAcessar(tenant, merchantId))
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        if (request.AppItemIds.Count == 0)
            return TypedResults.Problem(
                title: "Informe ao menos um item.",
                detail: "catalogo.itens_vazios",
                statusCode: StatusCodes.Status400BadRequest);

        var merchant = await merchants.ObterPorIdAsync(merchantId, ct);
        if (merchant is null)
            return ProblemaDe(ConexaoIFoodErrors.MerchantNaoEncontrado);

        if (string.IsNullOrWhiteSpace(merchant.NoventaENoveAppShopId))
            return TypedResults.Problem(
                title: "Restaurante sem vínculo com a 99Food.",
                detail: "merchant.sem_vinculo_99food",
                statusCode: StatusCodes.Status409Conflict);

        try
        {
            var resultado = await catalogo.DefinirDisponibilidadeAsync(
                merchant.NoventaENoveAppShopId,
                merchant.NoventaENoveAppId,
                request.AppItemIds,
                request.Disponivel,
                ct);

            // A 99 aceita o lote parcialmente. Devolver só "ok" esconderia os
            // itens que continuaram como estavam.
            return TypedResults.Ok(new DisponibilidadeResponse(
                resultado.Sucesso, resultado.Falha));
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            return TypedResults.Problem(
                title: "Não foi possível alterar a disponibilidade na 99Food.",
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static ItemCardapioResponse ParaResponse(ItemDoCardapio99 item) => new(
        item.AppItemId, item.Nome, item.Descricao, item.Preco,
        item.Disponivel, item.VendidoSeparadamente, item.ImagemUrl);

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
