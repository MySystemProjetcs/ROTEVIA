using DeliveryHub.Domain.Identity;

namespace DeliveryHub.Application.Identity;

// Dados da loja — só existem quando quem abriu o perfil é o dono.
public sealed record PerfilDaLoja(
    string Nome,
    string? Endereco,
    decimal TaxaPorEntrega);

// Dados do motoboy. CPF e telefone são do próprio dono da sessão: a consulta é
// por UsuarioId, nunca por parâmetro vindo da URL.
public sealed record PerfilDoEntregador(
    string Cpf,
    string Telefone,
    string ModeloDaMoto,
    string Placa,
    bool DisponivelParaEntrega);

public sealed record Perfil(
    string Nome,
    string Email,
    PapelUsuario Papel,
    string? FotoBase64,
    DateTimeOffset MembroDesde,
    PerfilDaLoja? Loja,
    PerfilDoEntregador? Entregador);

public interface IObterPerfil
{
    Task<Perfil?> ExecutarAsync(Guid usuarioId, CancellationToken ct);
}
