using DeliveryHub.Domain.Identity;

namespace DeliveryHub.Application.Abstractions;

public sealed record TokenEmitido(string AccessToken, DateTimeOffset ExpiraEm);

public interface IGeradorDeToken
{
    // merchantId é nulo para o administrador do sistema, que não opera dentro
    // de uma loja.
    TokenEmitido Gerar(Usuario usuario, Guid? merchantId);
}
