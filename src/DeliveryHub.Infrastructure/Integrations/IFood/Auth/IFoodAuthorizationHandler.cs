using System.Net;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Auth;

// Anexa o Authorization em toda chamada de recurso e, no 401, troca o token e
// repete uma vez. A doc do iFood manda tratar 401 como "peça token novo" — o
// prazo de expiração pode mudar sem aviso, então o relógio local não basta.
internal sealed class IFoodAuthorizationHandler : DelegatingHandler
{
    private readonly IIFoodAuthenticator _authenticator;

    public IFoodAuthorizationHandler(IIFoodAuthenticator authenticator)
    {
        _authenticator = authenticator;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = await _authenticator.GetAuthorizationHeaderAsync(cancellationToken);

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        response.Dispose();
        _authenticator.InvalidateCache();

        // HttpRequestMessage não é reaproveitável com segurança depois de enviado.
        var retry = await CloneAsync(request, cancellationToken);
        retry.Headers.Authorization = await _authenticator.GetAuthorizationHeaderAsync(cancellationToken);

        return await base.SendAsync(retry, cancellationToken);
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri) { Version = request.Version };

        if (request.Content is not null)
        {
            clone.Content = new ByteArrayContent(await request.Content.ReadAsByteArrayAsync(ct));
            foreach (var header in request.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return clone;
    }
}
