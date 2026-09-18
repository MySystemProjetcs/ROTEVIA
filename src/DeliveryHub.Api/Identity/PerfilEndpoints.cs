using DeliveryHub.Application.Abstractions;
using DeliveryHub.Application.Identity;
using DeliveryHub.Domain.SharedKernel;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DeliveryHub.Api.Identity;

public sealed record PerfilLojaResponse(string Nome, string? Endereco, decimal TaxaPorEntrega);

public sealed record PerfilEntregadorResponse(
    string Cpf,
    string Telefone,
    string ModeloDaMoto,
    string Placa,
    bool DisponivelParaEntrega);

public sealed record PerfilResponse(
    string Nome,
    string Email,
    string Papel,
    string? FotoBase64,
    DateTimeOffset MembroDesde,
    PerfilLojaResponse? Loja,
    PerfilEntregadorResponse? Entregador);

public sealed record DefinirFotoRequest(string FotoBase64);

public static class PerfilEndpoints
{
    public static void MapPerfilEndpoints(this IEndpointRouteBuilder app)
    {
        // Sem policy de papel: todo usuário autenticado tem um perfil, e o que
        // ele enxerga é sempre o dele — o id sai do token.
        var group = app.MapGroup("/api/perfil")
            .WithTags("Perfil")
            .RequireAuthorization();

        group.MapGet("/", Obter);
        group.MapPut("/foto", DefinirFoto);
    }

    private static async Task<Results<Ok<PerfilResponse>, ProblemHttpResult>> Obter(
        ITenantContext tenant,
        IObterPerfil perfis,
        CancellationToken ct)
    {
        if (tenant.UsuarioId is not { } usuarioId)
            return ProblemaDe(AutenticacaoErrors.CredenciaisInvalidas);

        var perfil = await perfis.ExecutarAsync(usuarioId, ct);
        if (perfil is null)
            return ProblemaDe(AutenticacaoErrors.CredenciaisInvalidas);

        return TypedResults.Ok(new PerfilResponse(
            perfil.Nome,
            perfil.Email,
            perfil.Papel.ToString(),
            perfil.FotoBase64,
            perfil.MembroDesde,
            perfil.Loja is null
                ? null
                : new PerfilLojaResponse(perfil.Loja.Nome, perfil.Loja.Endereco, perfil.Loja.TaxaPorEntrega),
            perfil.Entregador is null
                ? null
                : new PerfilEntregadorResponse(
                    perfil.Entregador.Cpf,
                    perfil.Entregador.Telefone,
                    perfil.Entregador.ModeloDaMoto,
                    perfil.Entregador.Placa,
                    perfil.Entregador.DisponivelParaEntrega)));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DefinirFoto(
        DefinirFotoRequest request,
        ITenantContext tenant,
        IDefinirFotoDePerfil definir,
        CancellationToken ct)
    {
        if (tenant.UsuarioId is not { } usuarioId)
            return ProblemaDe(AutenticacaoErrors.CredenciaisInvalidas);

        var resultado = await definir.ExecutarAsync(usuarioId, request.FotoBase64, ct);

        return resultado.IsSuccess ? TypedResults.NoContent() : ProblemaDe(resultado.Error);
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
