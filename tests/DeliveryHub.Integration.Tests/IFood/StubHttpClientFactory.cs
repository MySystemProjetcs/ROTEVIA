namespace DeliveryHub.Integration.Tests.IFood;

internal sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;
    private readonly string _baseAddress;

    public StubHttpClientFactory(HttpMessageHandler handler, string baseAddress)
    {
        _handler = handler;
        _baseAddress = baseAddress;
    }

    public HttpClient CreateClient(string name) =>
        new(_handler, disposeHandler: false) { BaseAddress = new Uri(_baseAddress) };
}
