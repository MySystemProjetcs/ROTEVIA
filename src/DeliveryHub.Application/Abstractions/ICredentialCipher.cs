namespace DeliveryHub.Application.Abstractions;

// Token de restaurante em texto plano no banco é o tipo de coisa que vaza
// junto num dump e dá acesso à conta de N lojas de uma vez. Nunca sem isso.
public interface ICredentialCipher
{
    string Proteger(string valor);
    string Desproteger(string valor);
}
