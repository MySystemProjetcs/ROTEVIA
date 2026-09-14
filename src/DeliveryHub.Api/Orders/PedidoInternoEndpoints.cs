using DeliveryHub.Application.Abstractions;
using DeliveryHub.Application.Orders;
using DeliveryHub.Domain.SharedKernel;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DeliveryHub.Api.Orders;

public sealed record ItemInternoRequest(
    string Nome,
    int Quantidade,
    decimal PrecoUnitario,
    string? Observacoes);

public sealed record PedidoInternoRequest(
    string ClienteNome,
    string? ClienteTelefone,
    string Cep,
    string Numero,
    string? Complemento,
    string? Referencia,
    string FormaPagamento,
    bool JaPago,
    IReadOnlyList<ItemInternoRequest> Itens,
    Guid? EntregadorId);

public sealed record PedidoInternoResponse(Guid PedidoId);

public static class PedidoInternoEndpoints
{
    public static void MapPedidoInternoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/pedidos-internos")
            .WithTags("Pedidos internos")
            .RequireAuthorization(Identity.Policies.OperadorDaLoja);

        group.MapPost("/", Lancar);

        // Consulta separada para o formulário preencher rua/bairro assim que o
        // CEP é digitado, sem esperar o envio.
        group.MapGet("/cep/{cep}", ConsultarCep);
    }

    private static async Task<Results<Ok<PedidoInternoResponse>, ProblemHttpResult>> Lancar(
        PedidoInternoRequest request,
        ILancarPedidoInterno lancar,
        INotificadorPainel notificador,
        ITenantContext tenant,
        CancellationToken ct)
    {
        var resultado = await lancar.ExecutarAsync(
            new NovoPedidoInterno(
                request.ClienteNome,
                request.ClienteTelefone,
                request.Cep,
                request.Numero,
                request.Complemento,
                request.Referencia,
                request.FormaPagamento,
                request.JaPago,
                request.Itens
                    .Select(i => new ItemDoLancamento(i.Nome, i.Quantidade, i.PrecoUnitario, i.Observacoes))
                    .ToList(),
                request.EntregadorId),
            ct);

        if (!resultado.IsSuccess)
            return ProblemaDe(resultado.Error);

        // Pedido novo muda receita e contagem do dia no painel.
        if (tenant.MerchantId is { } merchantId)
            await notificador.ResumoAtualizadoAsync(merchantId, ct);

        return TypedResults.Ok(new PedidoInternoResponse(resultado.Value));
    }

    private static async Task<Results<Ok<EnderecoResolvido>, NotFound>> ConsultarCep(
        string cep,
        IResolverEndereco enderecos,
        CancellationToken ct)
    {
        // Número vazio: aqui só interessa rua/bairro/cidade para preencher o
        // formulário. A coordenada definitiva sai no lançamento, já com número.
        var endereco = await enderecos.PorCepAsync(cep, string.Empty, ct);

        return endereco is null ? TypedResults.NotFound() : TypedResults.Ok(endereco);
    }

    private static ProblemHttpResult ProblemaDe(Error erro) => TypedResults.Problem(
        title: erro.Message,
        detail: erro.Code,
        statusCode: erro.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        });
}
