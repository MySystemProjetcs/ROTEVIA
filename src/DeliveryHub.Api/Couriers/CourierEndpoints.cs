using DeliveryHub.Application.Abstractions;
using DeliveryHub.Application.Couriers;
using DeliveryHub.Domain.SharedKernel;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DeliveryHub.Api.Couriers;

public sealed record ConvidarEntregadorRequest(
    string Cpf, string Nome, string Telefone, string ModeloDaMoto, string Placa, string Email);

public sealed record EntregadorConvidadoResponse(Guid CourierId, Guid LinkId, string ConviteUrl, bool EnviadoPeloWhatsApp);

public sealed record EntregadorListadoResponse(
    Guid LinkId, Guid CourierId, string Nome, string Telefone,
    string ModeloDaMoto, string Placa, string Status, bool Disponivel);

public sealed record ConfirmarConviteRequest(string Token, string Senha);

public sealed record PreviaDoConviteResponse(string NomeEntregador, string NomeLoja, bool Valido);

public static class CourierEndpoints
{
    public static void MapCourierEndpoints(this IEndpointRouteBuilder app)
    {
        var restaurantes = app.MapGroup("/api/restaurantes")
            .WithTags("Entregadores")
            .RequireAuthorization(Identity.Policies.OperadorDaLoja);

        restaurantes.MapPost("/{merchantId:guid}/entregadores", Convidar);
        restaurantes.MapGet("/{merchantId:guid}/entregadores", Listar);

        // Convite: sem sessão, o motoboy ainda não existe como usuário.
        var convites = app.MapGroup("/api/convites/entregador").WithTags("Entregadores").AllowAnonymous();

        convites.MapGet("/{linkId:guid}", ObterPrevia);
        convites.MapPost("/{linkId:guid}/confirmar", Confirmar);
    }

    private static async Task<Results<Ok<EntregadorConvidadoResponse>, ProblemHttpResult>> Convidar(
        Guid merchantId,
        ConvidarEntregadorRequest request,
        ITenantContext tenant,
        IConvidarEntregador convidar,
        CancellationToken ct)
    {
        if (!PodeAcessar(tenant, merchantId))
            return ProblemaDe(CourierErrorsNotFound);

        var resultado = await convidar.ExecutarAsync(
            merchantId, request.Cpf, request.Nome, request.Telefone,
            request.ModeloDaMoto, request.Placa, request.Email, ct);

        if (resultado.IsFailure)
            return ProblemaDe(resultado.Error);

        return TypedResults.Ok(new EntregadorConvidadoResponse(
            resultado.Value.CourierId, resultado.Value.LinkId, resultado.Value.ConviteUrl, resultado.Value.EnviadoPeloWhatsApp));
    }

    private static async Task<Results<Ok<IReadOnlyList<EntregadorListadoResponse>>, ProblemHttpResult>> Listar(
        Guid merchantId,
        ITenantContext tenant,
        IListarEntregadores listar,
        CancellationToken ct)
    {
        if (!PodeAcessar(tenant, merchantId))
            return ProblemaDe(CourierErrorsNotFound);

        var entregadores = await listar.ExecutarAsync(merchantId, ct);

        IReadOnlyList<EntregadorListadoResponse> resposta = entregadores
            .Select(x => new EntregadorListadoResponse(
                x.LinkId, x.CourierId, x.Nome, x.Telefone, x.ModeloDaMoto, x.Placa, x.Status.ToString(), x.Disponivel))
            .ToList();

        return TypedResults.Ok(resposta);
    }

    private static async Task<Results<Ok<PreviaDoConviteResponse>, ProblemHttpResult>> ObterPrevia(
        Guid linkId, IObterPreviaDoConvite obterPrevia, CancellationToken ct)
    {
        var resultado = await obterPrevia.ExecutarAsync(linkId, ct);

        return resultado.IsSuccess
            ? TypedResults.Ok(new PreviaDoConviteResponse(
                resultado.Value.NomeEntregador, resultado.Value.NomeLoja, resultado.Value.Valido))
            : ProblemaDe(resultado.Error);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Confirmar(
        Guid linkId, ConfirmarConviteRequest request, IConfirmarConviteEntregador confirmar, CancellationToken ct)
    {
        var resultado = await confirmar.ExecutarAsync(linkId, request.Token, request.Senha, ct);

        return resultado.IsSuccess ? TypedResults.NoContent() : ProblemaDe(resultado.Error);
    }

    // Mesma lógica do MerchantEndpoints: 404 em vez de 403, pra não revelar
    // que a loja alheia existe.
    private static bool PodeAcessar(ITenantContext tenant, Guid merchantId) =>
        tenant.PodeVerTodosOsTenants || tenant.MerchantId == merchantId;

    private static readonly Error CourierErrorsNotFound = new(
        "restaurante.nao_encontrado", "Restaurante não encontrado.", ErrorType.NotFound);

    private static ProblemHttpResult ProblemaDe(Error erro) => TypedResults.Problem(
        title: erro.Message,
        detail: erro.Code,
        statusCode: erro.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError
        });
}
