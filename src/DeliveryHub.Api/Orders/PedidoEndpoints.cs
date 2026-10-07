using DeliveryHub.Application.Orders;
using DeliveryHub.Domain.SharedKernel;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DeliveryHub.Api.Orders;

public sealed record AlocarEntregadorRequest(Guid EntregadorId);

public sealed record CancelarPedidoRequest(string Motivo);

public sealed record DespacharEmLoteRequest(Guid EntregadorId, IReadOnlyList<Guid> PedidoIds);

public static class PedidoEndpoints
{
    public static void MapPedidoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/pedidos")
            .WithTags("Pedidos")
            .RequireAuthorization(Identity.Policies.OperadorDaLoja);

        group.MapGet("/", Listar);

        // Um caminho por ação em vez de um PATCH com o status no corpo: assim a
        // transição válida é decidida pelo domínio, não por um campo que o
        // cliente preenche.
        group.MapPost("/{id:guid}/confirmar", (Guid id, IAvancarPedido a, CancellationToken ct) =>
            Avancar(id, AcaoDePedido.Confirmar, a, ct));

        group.MapPost("/{id:guid}/iniciar-preparo", (Guid id, IAvancarPedido a, CancellationToken ct) =>
            Avancar(id, AcaoDePedido.IniciarPreparo, a, ct));

        group.MapPost("/{id:guid}/pronto", (Guid id, IAvancarPedido a, CancellationToken ct) =>
            Avancar(id, AcaoDePedido.MarcarPronto, a, ct));

        // Só define quem vai entregar — não move o status. Precisa acontecer
        // antes do /despachar, que agora exige entregador já alocado.
        group.MapPost("/{id:guid}/alocar-entregador", AlocarEntregador);

        group.MapPost("/{id:guid}/despachar", (Guid id, IAvancarPedido a, CancellationToken ct) =>
            Avancar(id, AcaoDePedido.Despachar, a, ct));

        // Cancelamento pelo dono, alcançável em qualquer status não terminal.
        // Motivo obrigatório (validado no caso de uso).
        group.MapPost("/{id:guid}/cancelar", Cancelar);

        // Pedidos casados: despacha vários pedidos numa corrida só, com o mesmo
        // motoboy e a ordem de paradas já calculada pelo sistema.
        group.MapPost("/despachar-lote", DespacharLote);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DespacharLote(
        DespacharEmLoteRequest request,
        IDespacharEmLote despachar,
        CancellationToken ct)
    {
        var resultado = await despachar.ExecutarAsync(request.EntregadorId, request.PedidoIds, ct);

        return resultado.IsSuccess
            ? TypedResults.NoContent()
            : TypedResults.Problem(
                title: resultado.Error.Message,
                detail: resultado.Error.Code,
                statusCode: ParaStatusHttp(resultado.Error.Type));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Cancelar(
        Guid id,
        CancelarPedidoRequest request,
        ICancelarPedido cancelar,
        CancellationToken ct)
    {
        var resultado = await cancelar.ExecutarAsync(id, request.Motivo, ct);

        return resultado.IsSuccess
            ? TypedResults.NoContent()
            : TypedResults.Problem(
                title: resultado.Error.Message,
                detail: resultado.Error.Code,
                statusCode: ParaStatusHttp(resultado.Error.Type));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> AlocarEntregador(
        Guid id, AlocarEntregadorRequest request, IAlocarEntregador alocar, CancellationToken ct)
    {
        var resultado = await alocar.ExecutarAsync(id, request.EntregadorId, ct);

        return resultado.IsSuccess
            ? TypedResults.NoContent()
            : TypedResults.Problem(
                title: resultado.Error.Message,
                detail: resultado.Error.Code,
                statusCode: ParaStatusHttp(resultado.Error.Type));
    }

    private static async Task<Ok<IReadOnlyList<PedidoDto>>> Listar(
        IListarPedidos listar,
        CancellationToken ct,
        bool apenasAtivos = true)
    {
        return TypedResults.Ok(await listar.ExecutarAsync(apenasAtivos, ct));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Avancar(
        Guid id,
        AcaoDePedido acao,
        IAvancarPedido avancar,
        CancellationToken ct)
    {
        var resultado = await avancar.ExecutarAsync(id, acao, ct);

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
}
