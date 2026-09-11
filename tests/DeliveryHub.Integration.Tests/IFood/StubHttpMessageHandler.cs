using System.Net;
using System.Net.Http.Headers;

namespace DeliveryHub.Integration.Tests.IFood;

// Substitui o WireMock enquanto o polling não existe: responde de uma fila e
// registra o que saiu, que é o que prova cache e retry funcionando.
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();

    public int RequestCount { get; private set; }
    public string LastRequestBody { get; private set; } = string.Empty;
    public Uri? LastRequestUri { get; private set; }
    public List<AuthenticationHeaderValue?> AuthorizationHeaders { get; } = [];

    public StubHttpMessageHandler Enqueue(HttpStatusCode status, string body)
    {
        _responses.Enqueue(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        });

        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        LastRequestUri = request.RequestUri;
        AuthorizationHeaders.Add(request.Headers.Authorization);

        if (request.Content is not null)
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);

        return _responses.Dequeue();
    }
}
