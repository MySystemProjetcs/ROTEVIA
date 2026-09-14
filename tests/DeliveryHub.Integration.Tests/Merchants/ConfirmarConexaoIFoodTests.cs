using DeliveryHub.Application.Abstractions;
using DeliveryHub.Application.Merchants;
using DeliveryHub.Domain.Merchants;
using DeliveryHub.Integration.Tests.IFood;

namespace DeliveryHub.Integration.Tests.Merchants;

public sealed class ConfirmarConexaoIFoodTests
{
    private sealed class FakeConnector : IIFoodMerchantConnector
    {
        private readonly Guid _merchantIdDescoberto;

        public FakeConnector(Guid merchantIdDescoberto) => _merchantIdDescoberto = merchantIdDescoberto;

        public Task<CodigoDeVinculoIFood> SolicitarCodigoAsync(CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<TokenDistribuidoIFood> TrocarPorTokenAsync(string codigo, string verifier, CancellationToken ct) =>
            Task.FromResult(new TokenDistribuidoIFood("token", "refresh", "bearer", DateTimeOffset.UtcNow.AddHours(3)));

        public Task<TokenDistribuidoIFood> RenovarTokenAsync(string refreshToken, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<Guid> DescobrirMerchantIdAsync(string accessToken, CancellationToken ct) =>
            Task.FromResult(_merchantIdDescoberto);
    }

    private sealed class FakeRepositorio : IMerchantRepository
    {
        private readonly Dictionary<Guid, Merchant> _porId = [];
        public HashSet<Guid> IFoodIdsJaUsados { get; } = [];
        public int Salvamentos { get; private set; }

        public void Adicionar(Merchant merchant) => _porId[merchant.Id] = merchant;

        public Task<Merchant?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(_porId.GetValueOrDefault(id));

        public Task<bool> ExistePorIFoodIdAsync(Guid ifoodMerchantId, Guid excetoMerchantId, CancellationToken ct) =>
            Task.FromResult(_porId.Values.Any(m => m.IFoodMerchantId == ifoodMerchantId && m.Id != excetoMerchantId));

        public Task<IReadOnlyList<Merchant>> ListarConectadosAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Merchant>>(_porId.Values.Where(m => m.ConexaoIFood?.Conectado == true).ToList());

        public Task SalvarAsync(CancellationToken ct) { Salvamentos++; return Task.CompletedTask; }
    }

    private static Merchant NovoMerchantComConexaoPendente(TestTimeProvider relogio)
    {
        var merchant = Merchant.Criar("Loja", relogio.GetUtcNow());
        merchant.IniciarConexaoIFood("userCode", "verifier", relogio.GetUtcNow().AddMinutes(10));
        return merchant;
    }

    [Fact]
    public async Task Confirma_e_persiste_o_merchant_id_descoberto()
    {
        var relogio = new TestTimeProvider();
        var merchant = NovoMerchantComConexaoPendente(relogio);
        var repositorio = new FakeRepositorio();
        repositorio.Adicionar(merchant);

        var ifoodId = Guid.CreateVersion7();
        var caso = new ConfirmarConexaoIFood(repositorio, new FakeConnector(ifoodId), relogio);

        var resultado = await caso.ExecutarAsync(merchant.Id, "codigo-de-autorizacao", CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ifoodId, merchant.IFoodMerchantId);
        Assert.True(merchant.ConexaoIFood!.Conectado);
        Assert.Equal(1, repositorio.Salvamentos);
    }

    [Fact]
    public async Task Recusa_loja_ja_vinculada_a_outro_restaurante()
    {
        var relogio = new TestTimeProvider();
        var ifoodId = Guid.CreateVersion7();

        var repositorio = new FakeRepositorio();

        var jaExistente = Merchant.Criar("Loja original", relogio.GetUtcNow());
        jaExistente.IniciarConexaoIFood("outro-code", "outro-verifier", relogio.GetUtcNow().AddMinutes(10));
        jaExistente.ConfirmarConexaoIFood(ifoodId, "tok", "ref", "bearer", relogio.GetUtcNow().AddHours(3), relogio.GetUtcNow());
        repositorio.Adicionar(jaExistente);

        var novoCadastro = NovoMerchantComConexaoPendente(relogio);
        repositorio.Adicionar(novoCadastro);

        var caso = new ConfirmarConexaoIFood(repositorio, new FakeConnector(ifoodId), relogio);
        var resultado = await caso.ExecutarAsync(novoCadastro.Id, "codigo-de-autorizacao", CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ConexaoIFoodErrors.LojaJaVinculadaAOutroCadastro, resultado.Error);
        Assert.Null(novoCadastro.IFoodMerchantId);
    }

    [Fact]
    public async Task ExistePorIFoodIdAsync_exclui_o_proprio_restaurante_da_comparacao()
    {
        // Bug real, achado ao tentar religar uma loja antiga (vinculada por
        // SQL manual antes do fluxo Distribuído existir): sem excluir o
        // próprio restaurante da comparação, a checagem de duplicidade
        // encontrava a própria loja como "já vinculada a outro cadastro" e
        // recusava a primeira conexão Distribuída dela.
        var relogio = new TestTimeProvider();
        var ifoodId = Guid.CreateVersion7();
        var repositorio = new FakeRepositorio();

        // Estado real da "Loja de Teste Jonathan": IFoodMerchantId já veio de
        // fora do fluxo Distribuído (SQL manual), ConexaoIFood nunca existiu.
        var merchant = Merchant.Criar("Loja de Teste Jonathan", relogio.GetUtcNow());
        merchant.IniciarConexaoIFood("code", "verifier", relogio.GetUtcNow().AddMinutes(10));
        merchant.ConfirmarConexaoIFood(ifoodId, "tok", "ref", "bearer", relogio.GetUtcNow().AddHours(3), relogio.GetUtcNow());
        repositorio.Adicionar(merchant);

        var resultado = await repositorio.ExistePorIFoodIdAsync(ifoodId, merchant.Id, CancellationToken.None);

        Assert.False(resultado);
    }

    [Fact]
    public async Task Restaurante_inexistente_devolve_nao_encontrado()
    {
        var relogio = new TestTimeProvider();
        var caso = new ConfirmarConexaoIFood(new FakeRepositorio(), new FakeConnector(Guid.CreateVersion7()), relogio);

        var resultado = await caso.ExecutarAsync(Guid.CreateVersion7(), "codigo", CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ConexaoIFoodErrors.MerchantNaoEncontrado, resultado.Error);
    }

    [Fact]
    public async Task Sem_conexao_iniciada_devolve_erro_e_nao_chama_o_conector()
    {
        var relogio = new TestTimeProvider();
        var merchant = Merchant.Criar("Loja", relogio.GetUtcNow());
        var repositorio = new FakeRepositorio();
        repositorio.Adicionar(merchant);

        var caso = new ConfirmarConexaoIFood(repositorio, new FakeConnector(Guid.CreateVersion7()), relogio);
        var resultado = await caso.ExecutarAsync(merchant.Id, "codigo", CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(DeliveryHub.Domain.Merchants.MerchantErrors.ConexaoNaoIniciada, resultado.Error);
    }
}
