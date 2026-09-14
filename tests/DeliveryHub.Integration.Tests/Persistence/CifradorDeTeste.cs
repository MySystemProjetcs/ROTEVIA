using DeliveryHub.Application.Abstractions;

namespace DeliveryHub.Integration.Tests.Persistence;

// Sem criptografia real: os testes de persistência não avaliam sigilo, só
// que o dado sobrevive ao ciclo de gravar e reler.
internal sealed class CifradorDeTeste : ICredentialCipher
{
    public string Proteger(string valor) => valor;
    public string Desproteger(string valor) => valor;
}
