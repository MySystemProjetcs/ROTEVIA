using DeliveryHub.Application.Identity;
using DeliveryHub.Domain.SharedKernel;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DeliveryHub.Api.Identity;

public sealed record LoginRequest(string Email, string Senha);
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiraEm, bool DeveTrocarSenha, string? NomeRestaurante, string NomeUsuario);

public sealed record CadastrarRestauranteRequest(string NomeLoja, string EmailDono, string NomeDono);

// A senha provisória trafega uma única vez, nesta resposta, para o
// administrador repassar ao dono. Não é persistida nem registrada em log.
public sealed record CadastrarRestauranteResponse(Guid MerchantId, Guid UsuarioId, string SenhaProvisoria);

public sealed record CriarDonoRequest(string Email, string Nome);
public sealed record CriarDonoResponse(Guid UsuarioId, string SenhaProvisoria);

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", Login).AllowAnonymous().WithTags("Auth");

        var admin = app.MapGroup("/api/admin")
            .WithTags("Admin")
            .RequireAuthorization(Policies.AdministradorSistema);

        admin.MapPost("/restaurantes", CadastrarRestaurante);
        admin.MapPost("/restaurantes/{merchantId:guid}/dono", CriarDono);
    }

    private static async Task<Results<Ok<CriarDonoResponse>, ProblemHttpResult>> CriarDono(
        Guid merchantId,
        CriarDonoRequest request,
        ICriarDonoParaRestaurante criarDono,
        CancellationToken ct)
    {
        var resultado = await criarDono.ExecutarAsync(merchantId, request.Email, request.Nome, ct);

        return resultado.IsSuccess
            ? TypedResults.Ok(new CriarDonoResponse(resultado.Value.UsuarioId, resultado.Value.SenhaProvisoria))
            : ProblemaDe(resultado.Error);
    }

    private static async Task<Results<Ok<LoginResponse>, ProblemHttpResult>> Login(
        LoginRequest request,
        IAutenticar autenticar,
        CancellationToken ct)
    {
        var resultado = await autenticar.ExecutarAsync(request.Email, request.Senha, ct);

        return resultado.IsSuccess
            ? TypedResults.Ok(new LoginResponse(
                resultado.Value.AccessToken, resultado.Value.ExpiraEm, resultado.Value.DeveTrocarSenha, resultado.Value.NomeRestaurante, resultado.Value.NomeUsuario))
            : ProblemaDe(resultado.Error);
    }

    private static async Task<Results<Ok<CadastrarRestauranteResponse>, ProblemHttpResult>> CadastrarRestaurante(
        CadastrarRestauranteRequest request,
        ICadastrarRestaurante cadastrar,
        CancellationToken ct)
    {
        var resultado = await cadastrar.ExecutarAsync(request.NomeLoja, request.EmailDono, request.NomeDono, ct);

        return resultado.IsSuccess
            ? TypedResults.Ok(new CadastrarRestauranteResponse(
                resultado.Value.MerchantId, resultado.Value.UsuarioId, resultado.Value.SenhaProvisoria))
            : ProblemaDe(resultado.Error);
    }

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
