using DeliveryHub.Application.Abstractions;
using DeliveryHub.Application.Orders;
using DeliveryHub.Domain.Couriers;
using DeliveryHub.Domain.Merchants;
using DeliveryHub.Domain.Orders;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Integration.Tests.Orders;

public sealed class DespacharEmLoteTests
{
    private static readonly Guid MerchantId = Guid.CreateVersion7();
    private static readonly Guid EntregadorId = Guid.CreateVersion7();

    private sealed class FakePedidos : IPedidoRepository
    {
        private readonly Dictionary<Guid, Pedido> _porId;
        public FakePedidos(IEnumerable<Pedido> pedidos) => _porId = pedidos.ToDictionary(p => p.Id);
        public int Salvamentos { get; private set; }

        public Task<Pedido?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(_porId.GetValueOrDefault(id));
        public Task<Pedido?> ObterParaEntregadorAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Pedido?>(null);
        public Task<Pedido?> ObterEntregaEmCursoAsync(Guid entregadorId, CancellationToken ct) =>
            Task.FromResult<Pedido?>(null);
        public Task<bool> TemEntregaAtivaAsync(Guid entregadorId, Guid merchantId, CancellationToken ct) =>
            Task.FromResult(false);
        public void Adicionar(Pedido pedido) => throw new NotSupportedException();
        public Task SalvarAsync(CancellationToken ct)
        {
            Salvamentos++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCouriers : ICourierRepository
    {
        private readonly bool _temVinculo;
        public FakeCouriers(bool temVinculo) => _temVinculo = temVinculo;
        public Task<bool> ExisteVinculoAtivoAsync(Guid courierId, Guid merchantId, CancellationToken ct) =>
            Task.FromResult(_temVinculo);
        public Task SalvarAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<Courier?> ObterPorCpfAsync(string cpf, CancellationToken ct) => throw new NotSupportedException();
        public Task<Courier?> ObterPorIdAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<Courier?> ObterPorUsuarioIdAsync(Guid usuarioId, CancellationToken ct) => throw new NotSupportedException();
        public void Adicionar(Courier courier) => throw new NotSupportedException();
        public Task<CourierMerchantLink?> ObterLinkAsync(Guid linkId, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ExisteVinculoAtivoOuPendenteAsync(Guid courierId, Guid merchantId, CancellationToken ct) => throw new NotSupportedException();
        public void AdicionarLink(CourierMerchantLink link) => throw new NotSupportedException();
        public void RemoverLink(CourierMerchantLink link) => throw new NotSupportedException();
        public Task<IReadOnlyList<EntregadorResumo>> ListarPorMerchantAsync(Guid merchantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> ListarMerchantIdsAtivosAsync(Guid courierId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeMerchants : IMerchantRepository
    {
        private readonly Merchant? _merchant;
        public FakeMerchants(Merchant? merchant) => _merchant = merchant;
        public Task<Merchant?> ObterPorIdAsync(Guid id, CancellationToken ct) => Task.FromResult(_merchant);

        public void Adicionar(Merchant merchant) => throw new NotSupportedException();
        public Task<bool> ExistePorIFoodIdAsync(Guid ifoodMerchantId, Guid excetoMerchantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Merchant>> ListarConectadosAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task SalvarAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeOrigem : IOrderSource
    {
        private readonly Result _resposta;
        public FakeOrigem(Result resposta) => _resposta = resposta;
        public List<string> Despachados { get; } = [];

        public Task<Result> DespacharAsync(string idExternoPedido, CancellationToken ct)
        {
            Despachados.Add(idExternoPedido);
            return Task.FromResult(_resposta);
        }

        public Task<Result> ConfirmarPedidoAsync(string id, CancellationToken ct) => Task.FromResult(Result.Success());
        public Task<Result> IniciarPreparoAsync(string id, CancellationToken ct) => Task.FromResult(Result.Success());
        public Task<Result> MarcarProntoAsync(string id, CancellationToken ct) => Task.FromResult(Result.Success());
        public Task<Result> CancelarPedidoAsync(string id, string motivo, CancellationToken ct) => Task.FromResult(Result.Success());
        public Task<Result<bool>> VerificarCodigoDeEntregaAsync(string id, string codigo, CancellationToken ct) => Task.FromResult(Result.Success(true));
        public Task<Result<bool>> ValidarCodigoDeColetaAsync(string id, string codigo, CancellationToken ct) => Task.FromResult(Result.Success(true));
    }

    private sealed class FakeNotificador : INotificadorPainel
    {
        public List<Guid> Avisados { get; } = [];
        public Task ResumoAtualizadoAsync(Guid merchantId, CancellationToken ct)
        {
            Avisados.Add(merchantId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTenant : ITenantContext
    {
        public bool EstaAutenticado => true;
        public Guid? UsuarioId => Guid.CreateVersion7();
        public Guid? MerchantId => DespacharEmLoteTests.MerchantId;
        public bool PodeVerTodosOsTenants => false;
    }

    private static Merchant LojaEm(double lat, double lng)
    {
        var merchant = Merchant.Criar("Loja Teste", DateTimeOffset.UtcNow);
        merchant.DefinirEndereco(new Endereco("Rua", "1", "Bairro", "Cidade", "SP", "00000-000", null, null, lat, lng));
        return merchant;
    }

    private static Pedido PedidoPronto(string numero, double lat, double lng)
    {
        var endereco = new Endereco("Rua Cliente", "10", "Bairro", "Cidade", "SP", "00000-000", null, null, lat, lng);
        var pedido = Pedido.Receber(
            merchantId: MerchantId,
            idExterno: Guid.NewGuid().ToString(),
            numeroExibicao: numero,
            ehTeste: true,
            cliente: new Cliente("Cliente", null, null),
            enderecoEntrega: endereco,
            valorTotal: 30m,
            taxaEntrega: 0m,
            criadoNaOrigemEm: DateTimeOffset.UtcNow,
            recebidoEm: DateTimeOffset.UtcNow);
        pedido.Confirmar();
        pedido.IniciarPreparo();
        pedido.MarcarPronto();
        return pedido;
    }

    private static DespacharEmLote Criar(
        IEnumerable<Pedido> pedidos, FakeOrigem origem, FakeNotificador notificador,
        bool temVinculo = true, Merchant? loja = null) =>
        new(new FakePedidos(pedidos), new FakeCouriers(temVinculo), new FakeMerchants(loja),
            origem, notificador, new FakeTenant());

    [Fact]
    public async Task Menos_de_dois_pedidos_e_rejeitado()
    {
        var p = PedidoPronto("1", 0, 1);
        var origem = new FakeOrigem(Result.Success());

        var resultado = await Criar([p], origem, new FakeNotificador(), loja: LojaEm(0, 0))
            .ExecutarAsync(EntregadorId, [p.Id], CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(DespacharEmLoteErrors.LoteMuitoPequeno, resultado.Error);
        Assert.Empty(origem.Despachados);
    }

    [Fact]
    public async Task Pedido_que_nao_esta_pronto_bloqueia_o_lote()
    {
        var pronto = PedidoPronto("1", 0, 1);
        var recebido = Pedido.Receber(MerchantId, Guid.NewGuid().ToString(), "2", true,
            new Cliente("C", null, null), null, 30m, 0m, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var origem = new FakeOrigem(Result.Success());

        var resultado = await Criar([pronto, recebido], origem, new FakeNotificador(), loja: LojaEm(0, 0))
            .ExecutarAsync(EntregadorId, [pronto.Id, recebido.Id], CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(DespacharEmLoteErrors.PedidoNaoDespachavel, resultado.Error);
        Assert.Empty(origem.Despachados);
    }

    [Fact]
    public async Task Despacha_todos_no_mesmo_lote_ordenados_por_proximidade()
    {
        // Coordenadas reais (não-zero): 0 é tratado como "sem coordenada"
        // (protege do 0,0 do sandbox do iFood).
        var longe = PedidoPronto("1", -23.50, -46.70);
        var perto = PedidoPronto("2", -23.50, -46.61);
        var repo = new FakePedidos([longe, perto]);
        var origem = new FakeOrigem(Result.Success());
        var notificador = new FakeNotificador();

        var caso = new DespacharEmLote(repo, new FakeCouriers(true), new FakeMerchants(LojaEm(-23.50, -46.60)),
            origem, notificador, new FakeTenant());

        // Passa na ordem "errada" (longe primeiro) de propósito: o sistema deve
        // reordenar para perto → longe.
        var resultado = await caso.ExecutarAsync(EntregadorId, [longe.Id, perto.Id], CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(StatusPedido.Despachado, perto.Status);
        Assert.Equal(StatusPedido.Despachado, longe.Status);
        Assert.Equal(EntregadorId, perto.EntregadorId);
        // Mesmo lote nos dois.
        Assert.NotNull(perto.LoteEntregaId);
        Assert.Equal(perto.LoteEntregaId, longe.LoteEntregaId);
        // Perto é a parada 1, longe a 2.
        Assert.Equal(1, perto.OrdemNaRota);
        Assert.Equal(2, longe.OrdemNaRota);
        Assert.Equal(1, repo.Salvamentos);
        Assert.Contains(MerchantId, notificador.Avisados);
        Assert.Equal(2, origem.Despachados.Count);
    }

    [Fact]
    public async Task Origem_recusando_aborta_o_lote_sem_persistir()
    {
        var a = PedidoPronto("1", 0, 1);
        var b = PedidoPronto("2", 0, 5);
        var repo = new FakePedidos([a, b]);
        var origem = new FakeOrigem(Result.Failure(new Error("origem.recusou", "Recusou", ErrorType.Conflict)));

        var caso = new DespacharEmLote(repo, new FakeCouriers(true), new FakeMerchants(LojaEm(0, 0)),
            origem, new FakeNotificador(), new FakeTenant());

        var resultado = await caso.ExecutarAsync(EntregadorId, [a.Id, b.Id], CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(StatusPedido.Pronto, a.Status);
        Assert.Equal(StatusPedido.Pronto, b.Status);
        Assert.Null(a.LoteEntregaId);
        Assert.Equal(0, repo.Salvamentos);
    }

    [Fact]
    public async Task Motoboy_sem_vinculo_ativo_e_rejeitado()
    {
        var a = PedidoPronto("1", 0, 1);
        var b = PedidoPronto("2", 0, 5);
        var origem = new FakeOrigem(Result.Success());

        var resultado = await Criar([a, b], origem, new FakeNotificador(), temVinculo: false, loja: LojaEm(0, 0))
            .ExecutarAsync(EntregadorId, [a.Id, b.Id], CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(CourierErrors.VinculoNaoEncontrado, resultado.Error);
        Assert.Empty(origem.Despachados);
    }
}
