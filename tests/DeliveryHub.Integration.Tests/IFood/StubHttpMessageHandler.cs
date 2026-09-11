using System.Net;

namespace DeliveryHub.Integration.Tests.IFood;

// Substitui o WireMock enquanto o polling não existe: responde de uma fila e
// conta quantas requisições saíram, que é o que prova o cache funcionando.
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();

    public int RequestCount { get; private set; }

    public StubHttpMessageHandler Enqueue(HttpStatusCode status, string body)
    {
        _responses.Enqueue(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        });

        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        return Task.FromResult(_responses.Dequeue());
    }
}
