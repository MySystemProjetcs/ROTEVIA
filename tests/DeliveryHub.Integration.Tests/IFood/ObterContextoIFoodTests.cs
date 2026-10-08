using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Merchants;
using DeliveryHub.Infrastructure.Integrations.IFood.Merchants;
using DeliveryHub.Integration.Tests.Fakes;

namespace DeliveryHub.Integration.Tests.IFood;

public sealed class ObterContextoIFoodTests
{
    private sealed class FakeMerchantRepository(Merchant merchant) : IMerchantRepository
    {
        public Task<bool> ExistePorIFoodIdAsync(Guid ifoodMerchantId, Guid excetoMerchantId, CancellationToken ct) =>
            Task.FromResult(false);

        public Task<Merchant?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(id == merchant.Id ? merchant : null);

        public Task<IReadOnlyList<Merchant>> ListarConectadosAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Merchant>>([merchant]);

        public void Adicionar(Merchant value) { }
        public Task SalvarAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeSnapshotStore : IIFoodMerchantSnapshotStore
    {
        public IFoodMerchantSnapshot? Snapshot { get; private set; }

        public Task<IFoodMerchantSnapshot?> ObterAsync(Guid merchantId, CancellationToken ct) =>
            Task.FromResult(Snapshot?.MerchantId == merchantId ? Snapshot : null);

        public Task SalvarAsync(IFoodMerchantSnapshot snapshot, CancellationToken ct)
        {
            Snapshot = snapshot;
            return Task.CompletedTask;
        }

        public Task<IFoodMerchantSnapshot?> AtualizarHorarioAsync(
            Guid merchantId, string openingHoursJson, DateTimeOffset updatedAt, CancellationToken ct)
        {
            if (Snapshot is null || Snapshot.MerchantId != merchantId)
                return Task.FromResult<IFoodMerchantSnapshot?>(null);

            Snapshot = Snapshot with { OpeningHoursJson = openingHoursJson, UpdatedAt = updatedAt };
            return Task.FromResult<IFoodMerchantSnapshot?>(Snapshot);
        }
    }

    private sealed class FakeGateway : IIFoodMerchantContextGateway
    {
        public int Chamadas { get; private set; }
        public bool Indisponivel { get; set; }
        public System.Net.HttpStatusCode? StatusCodeDeFalha { get; set; }

        public Task<IFoodMerchantData> ObterAsync(Merchant merchant, CancellationToken ct)
        {
            Chamadas++;
            if (StatusCodeDeFalha is { } statusCode)
                throw new HttpRequestException("Falha integrada", null, statusCode);

            if (Indisponivel)
                throw new HttpRequestException("iFood indisponível no teste");

            return Task.FromResult(new IFoodMerchantData("{\"name\":\"Loja\"}", "[]", "[]"));
        }
    }

    private static Merchant MerchantConectado(TestTimeProvider clock)
    {
        var merchant = Merchant.Criar("Loja", clock.GetUtcNow());
        merchant.IniciarConexaoIFood("codigo", "verifier", clock.GetUtcNow().AddMinutes(10));
        merchant.ConfirmarConexaoIFood(
            Guid.CreateVersion7(), "access", "refresh", "bearer",
            clock.GetUtcNow().AddHours(1), clock.GetUtcNow());
        return merchant;
    }

    [Fact]
    public async Task Cache_reutiliza_dados_antes_de_15_minutos_e_renova_ao_vencer()
    {
        var clock = new TestTimeProvider();
        var merchant = MerchantConectado(clock);
        var gateway = new FakeGateway();
        var caso = CriarCaso(merchant, clock, new FakeSnapshotStore(), gateway);

        var primeiro = await caso.ExecutarAsync(merchant.Id, CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(14));
        var segundo = await caso.ExecutarAsync(merchant.Id, CancellationToken.None);
        Assert.Equal(1, gateway.Chamadas);

        clock.Advance(TimeSpan.FromMinutes(2));
        var terceiro = await caso.ExecutarAsync(merchant.Id, CancellationToken.None);

        Assert.True(primeiro.IsSuccess);
        Assert.True(segundo.IsSuccess);
        Assert.True(terceiro.IsSuccess);
        Assert.Equal(2, gateway.Chamadas);
    }

    [Fact]
    public async Task Falha_do_ifood_devolve_snapshot_anterior_marcado_como_desatualizado()
    {
        var clock = new TestTimeProvider();
        var merchant = MerchantConectado(clock);
        var gateway = new FakeGateway();
        var snapshots = new FakeSnapshotStore();
        var caso = CriarCaso(merchant, clock, snapshots, gateway);

        var inicial = await caso.ExecutarAsync(merchant.Id, CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(16));
        gateway.Indisponivel = true;
        var atualizado = await caso.ExecutarAsync(merchant.Id, CancellationToken.None);

        Assert.True(inicial.IsSuccess);
        Assert.True(atualizado.IsSuccess);
        Assert.True(atualizado.Value.IsStale);
        Assert.Equal("{\"name\":\"Loja\"}", atualizado.Value.DetailsJson);
        Assert.Equal(2, gateway.Chamadas);

        var staleDoCache = await caso.ExecutarAsync(merchant.Id, CancellationToken.None);
        Assert.True(staleDoCache.IsSuccess);
        Assert.True(staleDoCache.Value.IsStale);
        Assert.Equal(2, gateway.Chamadas);
    }

    [Theory]
    [InlineData(System.Net.HttpStatusCode.Unauthorized)]
    [InlineData(System.Net.HttpStatusCode.Forbidden)]
    public async Task Falha_de_autenticacao_nao_devolve_snapshot_stale(System.Net.HttpStatusCode statusCode)
    {
        var clock = new TestTimeProvider();
        var merchant = MerchantConectado(clock);
        var snapshots = new FakeSnapshotStore();
        await snapshots.SalvarAsync(
            new IFoodMerchantSnapshot(merchant.Id, "{}", "[]", "[]", clock.GetUtcNow().AddMinutes(-20)),
            CancellationToken.None);
        var gateway = new FakeGateway { StatusCodeDeFalha = statusCode };
        var caso = CriarCaso(merchant, clock, snapshots, gateway);

        var resultado = await caso.ExecutarAsync(merchant.Id, CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(IFoodMerchantContextErrors.AutenticacaoIntegradaFalhou, resultado.Error);
        Assert.Equal(
            "HOUVE ERRO NA SUA AUTENTICAÇÃO INTEGRADA. ENTRE EM CONTATO COM O SUPORTE.",
            resultado.Error.Message);
    }

    private static ObterContextoIFood CriarCaso(
        Merchant merchant,
        TestTimeProvider clock,
        IIFoodMerchantSnapshotStore snapshots,
        IIFoodMerchantContextGateway gateway) =>
        new(
            new FakeMerchantRepository(merchant),
            snapshots,
            gateway,
            new IFoodMerchantContextCache(clock),
            clock);
}