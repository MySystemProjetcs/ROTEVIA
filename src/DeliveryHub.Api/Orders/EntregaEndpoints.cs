using DeliveryHub.Application.Abstractions;
using DeliveryHub.Application.Couriers;
using DeliveryHub.Application.Orders;
using DeliveryHub.Application.Tracking;
using DeliveryHub.Domain.Orders;
using DeliveryHub.Infrastructure.RealTime;
using DeliveryHub.Domain.SharedKernel;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;

namespace DeliveryHub.Api.Orders;

// Body do ping de GPS enviado pelo app do motoboy.
public sealed record RegistrarPosicaoRequest(
    double Latitude,
    double Longitude,
    double PrecisaoEmMetros,
    DateTimeOffset CapturadoEm,
    bool EhHeartbeat = false);

public sealed record DefinirDisponibilidadeRequest(bool Disponivel);

// O código de 4 dígitos que o cliente informa ao entregador na porta.
public sealed record CodigoDeEntregaRequest(string Codigo);
public sealed record DisponibilidadeResponse(bool Disponivel);
public sealed record ItemGanhoResponse(
    Guid PedidoId,
    string NumeroExibicao,
    string NomeLoja,
    decimal Valor,
    DateTimeOffset RecebidoEm,
    string ClienteNome,
    string? EnderecoResumido,
    IReadOnlyList<string> Itens);
public sealed record GanhosResponse(
    decimal Total,
    int QtdEntregas,
    IReadOnlyList<ItemGanhoResponse> Itens,
    int Pagina,
    int TamanhoPagina);

public static class EntregaEndpoints
{
    public static void MapEntregaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/entregador")
            .WithTags("Entregas")
            .RequireAuthorization(Identity.Policies.Entregador);

        group.MapGet("/pedidos", Listar);

        group.MapGet("/disponibilidade", ObterDisponibilidade);
        group.MapPost("/disponibilidade", DefinirDisponibilidade);
        group.MapGet("/ganhos", Ganhos);

        group.MapPost("/pedidos/{id:guid}/aceitar", (Guid id, ITenantContext t, IAvancarEntrega a, CancellationToken ct) =>
            Avancar(t, id, AcaoDeEntrega.Aceitar, a, ct));

        group.MapPost("/pedidos/{id:guid}/sair-para-entrega", (Guid id, ITenantContext t, IAvancarEntrega a, CancellationToken ct) =>
            Avancar(t, id, AcaoDeEntrega.SairParaEntrega, a, ct));

        group.MapPost("/pedidos/{id:guid}/cheguei", (Guid id, ITenantContext t, IAvancarEntrega a, CancellationToken ct) =>
            Avancar(t, id, AcaoDeEntrega.ChegarNoLocal, a, ct));

        // Só existe em pedido com valor pendente — o domínio recusa em pedido
        // já pago.
        group.MapPost("/pedidos/{id:guid}/cobrar", (Guid id, ITenantContext t, IAvancarEntrega a, CancellationToken ct) =>
            Avancar(t, id, AcaoDeEntrega.Cobrar, a, ct));

        // Único do motoboy com corpo: o código vai no body, nunca na URL —
        // é dado do cliente e URL vaza em log de proxy (CLAUDE.md §10).
        group.MapPost("/pedidos/{id:guid}/confirmar-codigo", ConfirmarCodigo);
        group.MapPost("/pedidos/{id:guid}/finalizar", (Guid id, ITenantContext t, IAvancarEntrega a, CancellationToken ct) =>
            Avancar(t, id, AcaoDeEntrega.Finalizar, a, ct));

        // O ping de GPS do motoboy: < 50 ms p95 (CLAUDE.md §8).
        //
        // Fora de /pedidos de propósito: o motoboy é transmitido enquanto está
        // online, tenha entrega ou não. Quem descobre se há entrega em curso —
        // e portanto se o trajeto deve ser gravado — é o servidor.
        group.MapPost("/posicao", RegistrarPosicao);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RegistrarPosicao(
        RegistrarPosicaoRequest request,
        ITenantContext tenant,
        IRegistrarPosicao registrar,
        IHubContext<RastreioHub> hub,
        CancellationToken ct)
    {
        var resultado = await registrar.ExecutarAsync(
            tenant.UsuarioId!.Value,
            request.Latitude,
            request.Longitude,
            request.PrecisaoEmMetros,
            request.CapturadoEm,
            request.EhHeartbeat,
            ct);

        if (!resultado.IsSuccess)
            return TypedResults.Problem(
                title: resultado.Error.Message,
                detail: resultado.Error.Code,
                statusCode: ParaStatusHttp(resultado.Error.Type));

        // Uma emissão por loja: o motoboy pode atender mais de um restaurante, e
        // cada um recebe no seu próprio grupo. Grupo isolado por MerchantId, que
        // é o que garante que uma loja não veja o motoboy a caminho de outra
        // (CLAUDE.md §6, isolamento multi-tenant).
        foreach (var posicao in resultado.Value)
        {
            await hub.Clients
                .Group(RastreioHub.GrupoDoMerchant(posicao.MerchantId))
                .SendAsync(RastreioHub.MetodoPosicaoAtualizada, posicao, ct);
        }

        return TypedResults.NoContent();
    }

    private static async Task<Ok<IReadOnlyList<PedidoDto>>> Listar(
        ITenantContext tenant, IListarMinhasEntregas listar, CancellationToken ct)
    {
        return TypedResults.Ok(await listar.ExecutarAsync(tenant.UsuarioId!.Value, ct));
    }

    private static async Task<Results<Ok<DisponibilidadeResponse>, ProblemHttpResult>> ObterDisponibilidade(
        ITenantContext tenant, IDisponibilidadeEntrega disponibilidade, CancellationToken ct)
    {
        var resultado = await disponibilidade.ObterAsync(tenant.UsuarioId!.Value, ct);

        return resultado.IsSuccess
            ? TypedResults.Ok(new DisponibilidadeResponse(resultado.Value))
            : ProblemaDe(resultado.Error);
    }

    private static async Task<Results<Ok<DisponibilidadeResponse>, ProblemHttpResult>> DefinirDisponibilidade(
        ITenantContext tenant,
        DefinirDisponibilidadeRequest request,
        IDisponibilidadeEntrega disponibilidade,
        INotificadorPainel notificador,
        CancellationToken ct)
    {
        var resultado = await disponibilidade.DefinirAsync(tenant.UsuarioId!.Value, request.Disponivel, ct);

        if (!resultado.IsSuccess)
            return ProblemaDe(resultado.Error);

        // A contagem de online muda em todas as lojas do motoboy: avisa cada
        // painel. Falha de transporte não desfaz a troca já persistida.
        foreach (var merchantId in resultado.Value)
            await notificador.ResumoAtualizadoAsync(merchantId, ct);

        var atual = await disponibilidade.ObterAsync(tenant.UsuarioId!.Value, ct);
        return TypedResults.Ok(new DisponibilidadeResponse(atual.IsSuccess && atual.Value));
    }

    private static async Task<Results<Ok<GanhosResponse>, ProblemHttpResult>> Ganhos(
        ITenantContext tenant,
        IObterGanhosEntregador ganhos,
        CancellationToken ct,
        DateOnly? inicio = null,
        DateOnly? fim = null,
        int pagina = 1)
    {
        // Sem intervalo informado, o dia de hoje — mesmo padrão que a tela abre.
        var hoje = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, FusoLoja).Date);

        var de = inicio ?? hoje;
        var ate = fim ?? hoje;

        if (ate < de)
            return ProblemaDe(IntervaloInvalido);

        var resultado = await ganhos.ExecutarAsync(
            tenant.UsuarioId!.Value, new IntervaloDeGanhos(de, ate), pagina, ct);

        return TypedResults.Ok(new GanhosResponse(
            resultado.Total,
            resultado.QtdEntregas,
            resultado.Itens
                .Select(x => new ItemGanhoResponse(
                    x.PedidoId, x.NumeroExibicao, x.NomeLoja, x.Valor, x.RecebidoEm,
                    x.ClienteNome, x.EnderecoResumido, x.Itens))
                .ToList(),
            resultado.Pagina,
            resultado.TamanhoPagina));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Avancar(
        ITenantContext tenant, Guid pedidoId, AcaoDeEntrega acao, IAvancarEntrega avancar, CancellationToken ct)
    {
        var resultado = await avancar.ExecutarAsync(tenant.UsuarioId!.Value, pedidoId, acao, ct);

        return resultado.IsSuccess
            ? TypedResults.NoContent()
            : TypedResults.Problem(
                title: resultado.Error.Message,
                detail: resultado.Error.Code,
                statusCode: ParaStatusHttp(resultado.Error.Type));
    }

    private static int ParaStatusHttp(ErrorType tipo) => tipo switch
    {
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        _ => StatusCodes.Status500InternalServerError
    };

    private static async Task<Results<NoContent, ProblemHttpResult>> ConfirmarCodigo(
        Guid id,
        CodigoDeEntregaRequest request,
        ITenantContext tenant,
        IConfirmarEntregaComCodigo confirmar,
        CancellationToken ct)
    {
        if (tenant.UsuarioId is not { } usuarioId)
            return ProblemaDe(PedidoErrors.EntregadorNaoPertenceAoPedido);

        var resultado = await confirmar.ExecutarAsync(usuarioId, id, request.Codigo, ct);

        return resultado.IsSuccess ? TypedResults.NoContent() : ProblemaDe(resultado.Error);
    }

    private static ProblemHttpResult ProblemaDe(Error erro) => TypedResults.Problem(
        title: erro.Message,
        detail: erro.Code,
        statusCode: ParaStatusHttp(erro.Type));

    private static readonly Error IntervaloInvalido = new(
        "ganhos.intervalo_invalido", "A data final não pode ser anterior à inicial.", ErrorType.Validation);

    // O dia do motoboy vira no fuso da loja, não em UTC.
    private static readonly TimeZoneInfo FusoLoja =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
}
