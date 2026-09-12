using DeliveryHub.Infrastructure.Integrations.IFood.Contracts;
using DeliveryHub.Infrastructure.Integrations.IFood.Polling;
using DeliveryHub.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeliveryHub.Integration.Tests.IFood;

public sealed class IFoodEventIngestorTests
{
    private static readonly Guid MerchantConhecido = Guid.Parse("323f53b3-356c-4af8-85b7-b71eff95dd72");
    private static readonly Guid NossoTenant = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private sealed class FakeEventsClient : IIFoodEventsClient
    {
        private readonly IReadOnlyList<IFoodEvent> _eventos;

        public FakeEventsClient(params IFoodEvent[] eventos) => _eventos = eventos;

        public List<Guid> Reconhecidos { get; } = [];
        public bool ReconheceuAntesDeGravar { get; private set; }
        public Func<bool>? InboxJaGravou { get; set; }

        public Task<IReadOnlyList<IFoodEvent>> PollAsync(CancellationToken ct) => Task.FromResult(_eventos);

        public Task AcknowledgeAsync(IReadOnlyList<Guid> eventIds, CancellationToken ct)
        {
            if (InboxJaGravou is not null && !InboxJaGravou())
                ReconheceuAntesDeGravar = true;

            Reconhecidos.AddRange(eventIds);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeInbox : IIntegrationInboxWriter
    {
        private readonly HashSet<string> _gravados = [];

        public List<(string EventId, Guid? MerchantId, string Payload)> Chamadas { get; } = [];

        public Task<bool> TentarGravarAsync(string source, string externalEventId, Guid externalMerchantId,
            Guid? merchantId, string payloadJson, CancellationToken ct)
        {
            Chamadas.Add((externalEventId, merchantId, payloadJson));
            return Task.FromResult(_gravados.Add(externalEventId));
        }
    }

    private sealed class FakeMerchantResolver : IMerchantResolver
    {
        public Task<Guid?> ResolverAsync(Guid ifoodMerchantId, CancellationToken ct) =>
            Task.FromResult(ifoodMerchantId == MerchantConhecido ? NossoTenant : (Guid?)null);
    }

    private static IFoodEvent Evento(string id, Guid merchantId) =>
        new(Guid.Parse(id), Guid.CreateVersion7(), merchantId, "PLC", "PLACED", "IFOOD", DateTimeOffset.UtcNow, null);

    private static IFoodEventIngestor Construir(FakeEventsClient eventos, FakeInbox inbox) =>
        new(eventos, inbox, new FakeMerchantResolver(), NullLogger<IFoodEventIngestor>.Instance);

    [Fact]
    public async Task Polling_vazio_nao_chama_acknowledgment()
    {
        var eventos = new FakeEventsClient();
        var resultado = await Construir(eventos, new FakeInbox()).IngerirAsync(CancellationToken.None);

        Assert.Empty(eventos.Reconhecidos);
        Assert.Equal(0, resultado.Recebidos);
    }

    [Fact]
    public async Task Reconhece_somente_depois_de_gravar_no_inbox()
    {
        // Reconhecer antes de persistir perde o pedido se o processo cair no
        // meio: o iFood considera entregue e nunca reenvia.
        var inbox = new FakeInbox();
        var eventos = new FakeEventsClient(Evento("d31bb701-3ffe-4529-83a9-1c8b130b568d", MerchantConhecido))
        {
            InboxJaGravou = () => inbox.Chamadas.Count > 0
        };

        await Construir(eventos, inbox).IngerirAsync(CancellationToken.None);

        Assert.False(eventos.ReconheceuAntesDeGravar);
        Assert.Single(eventos.Reconhecidos);
    }

    [Fact]
    public async Task Merchant_conhecido_grava_com_o_tenant_resolvido()
    {
        var inbox = new FakeInbox();
        var resultado = await Construir(
            new FakeEventsClient(Evento("d31bb701-3ffe-4529-83a9-1c8b130b568d", MerchantConhecido)),
            inbox).IngerirAsync(CancellationToken.None);

        Assert.Equal(NossoTenant, inbox.Chamadas[0].MerchantId);
        Assert.Equal(0, resultado.EmQuarentena);
    }

    [Fact]
    public async Task Merchant_desconhecido_vai_para_quarentena_mas_e_reconhecido()
    {
        // Sem o ack, o evento volta para sempre no polling e polui todo ciclo.
        var inbox = new FakeInbox();
        var eventos = new FakeEventsClient(Evento("d31bb701-3ffe-4529-83a9-1c8b130b568d", Guid.CreateVersion7()));

        var resultado = await Construir(eventos, inbox).IngerirAsync(CancellationToken.None);

        Assert.Null(inbox.Chamadas[0].MerchantId);
        Assert.Equal(1, resultado.EmQuarentena);
        Assert.Single(eventos.Reconhecidos);
    }

    [Fact]
    public async Task Evento_repetido_e_contado_como_duplicado_e_reconhecido_de_novo()
    {
        var inbox = new FakeInbox();
        var ingestor = Construir(
            new FakeEventsClient(Evento("d31bb701-3ffe-4529-83a9-1c8b130b568d", MerchantConhecido)), inbox);

        await ingestor.IngerirAsync(CancellationToken.None);

        var eventosSegundaVez = new FakeEventsClient(Evento("d31bb701-3ffe-4529-83a9-1c8b130b568d", MerchantConhecido));
        var segunda = await Construir(eventosSegundaVez, inbox).IngerirAsync(CancellationToken.None);

        Assert.Equal(1, segunda.Duplicados);
        Assert.Equal(0, segunda.Gravados);
        Assert.Single(eventosSegundaVez.Reconhecidos);
    }

    [Fact]
    public async Task Payload_gravado_preserva_campo_que_o_iFood_adicione_sem_avisar()
    {
        var inbox = new FakeInbox();
        var comCampoNovo = Evento("d31bb701-3ffe-4529-83a9-1c8b130b568d", MerchantConhecido) with
        {
            CamposNaoMapeados = new() { ["campoFuturo"] = System.Text.Json.JsonDocument.Parse("\"valor\"").RootElement }
        };

        await Construir(new FakeEventsClient(comCampoNovo), inbox).IngerirAsync(CancellationToken.None);

        Assert.Contains("campoFuturo", inbox.Chamadas[0].Payload);
    }
}
