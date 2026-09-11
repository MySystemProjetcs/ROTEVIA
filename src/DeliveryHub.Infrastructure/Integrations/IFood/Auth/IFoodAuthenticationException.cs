namespace DeliveryHub.Infrastructure.Integrations.IFood.Auth;

// Falha de autenticação OAuth é infraestrutura externa falhando, não uma
// regra de negócio — por isso exceção, não Result<T> (ENGINEERING-GUIDE §3).
// O worker de polling decide como reagir (retry/backoff, alerta).
public sealed class IFoodAuthenticationException : Exception
{
    public string? IFoodErrorCode { get; }

    public IFoodAuthenticationException(string message, string? ifoodErrorCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        IFoodErrorCode = ifoodErrorCode;
    }
}
