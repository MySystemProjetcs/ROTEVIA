using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Merchants;
using DeliveryHub.Infrastructure.Integrations.IFood.Merchants;

namespace DeliveryHub.Integration.Tests.IFood;

public sealed class IFoodMerchantTokenProviderTests
{
    private sealed class FakeConnector : IIFoodMerchantConnector
    {
        public int ChamadasDeRenovacao { get; private set; }

        public Task<CodigoDeVinculoIFood> SolicitarCodigoAsync(CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<TokenDistribuidoIFood> TrocarPorTokenAsync(string c, string v, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<TokenDistribuidoIFood> RenovarTokenAsync(string refreshToken, CancellationToken ct)
        {
            ChamadasDeRenovacao++;
            return Task.FromResult(new TokenDistribuidoIFood("token-novo", "refresh-novo", "bearer", DateTimeOffset.MaxValue));
        }

        public Task<Guid> DescobrirMerchantIdAsync(string accessToken, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class FakeRepositorio : IMerchantRepository
    {
        public int Salvamentos { get; private set; }

        public Task<bool> ExistePorIFoodIdAsync(Guid id, Guid exceto, CancellationToken ct) => Task.FromResult(false);
        public Task<Merchant?> ObterPorIdAsync(Guid id, CancellationToken ct) => Task.FromResult<Merchant?>(null);
        public Task<IReadOnlyList<Merchant>> ListarConectadosAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Merchant>>([]);
        public void Adicionar(Merchant merchant) { }
        public Task SalvarAsync(CancellationToken ct) { Salvamentos++; return Task.CompletedTask; }
    }

    private static Merchant MerchantConectado(TestTimeProvider relogio, TimeSpan tempoAteExpirar)
    {
        var merchant = Merchant.Criar("Loja", relogio.GetUtcNow());
        merchant.IniciarConexaoIFood("codigo", "verifier", relogio.GetUtcNow().AddMinutes(10));
        merchant.ConfirmarConexaoIFood(
            Guid.CreateVersion7(), "token-atual", "refresh-atual", "bearer",
            relogio.GetUtcNow() + tempoAteExpirar, relogio.GetUtcNow());

        return merchant;
    }

    [Fact]
    public async Task Token_com_folga_nao_renova()
    {
        var relogio = new TestTimeProvider();
        var merchant = MerchantConectado(relogio, TimeSpan.FromMinutes(30));
        var connector = new FakeConnector();
        var provider = new IFoodMerchantTokenProvider(connector, new FakeRepositorio(), relogio);

        var autorizacao = await provider.ObterAsync(merchant, CancellationToken.None);

        Assert.Equal(0, connector.ChamadasDeRenovacao);
        Assert.Equal("token-atual", autorizacao.Parameter);
    }

    [Fact]
    public async Task Token_perto_de_expirar_renova_e_persiste()
    {
        // A doc de homologação do Order/Events pede renovação só quando
        // necessário, não com margem larga — por isso a janela aqui é curta
        // (60s), diferente dos 90% do autenticador Centralizado.
        var relogio = new TestTimeProvider();
        var merchant = MerchantConectado(relogio, TimeSpan.FromSeconds(30));
        var connector = new FakeConnector();
        var repositorio = new FakeRepositorio();
        var provider = new IFoodMerchantTokenProvider(connector, repositorio, relogio);

        var autorizacao = await provider.ObterAsync(merchant, CancellationToken.None);

        Assert.Equal(1, connector.ChamadasDeRenovacao);
        Assert.Equal("token-novo", autorizacao.Parameter);
        Assert.Equal(1, repositorio.Salvamentos);
        Assert.Equal("token-novo", merchant.ConexaoIFood!.AccessToken);
    }

    [Fact]
    public async Task Token_ja_expirado_renova()
    {
        var relogio = new TestTimeProvider();
        var merchant = MerchantConectado(relogio, TimeSpan.FromSeconds(-10));
        var connector = new FakeConnector();
        var provider = new IFoodMerchantTokenProvider(connector, new FakeRepositorio(), relogio);

        await provider.ObterAsync(merchant, CancellationToken.None);

        Assert.Equal(1, connector.ChamadasDeRenovacao);
    }
}
