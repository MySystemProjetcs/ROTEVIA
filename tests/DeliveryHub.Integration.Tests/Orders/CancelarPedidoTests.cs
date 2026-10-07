using DeliveryHub.Application.Abstractions;
using DeliveryHub.Application.Orders;
using DeliveryHub.Domain.Orders;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Integration.Tests.Orders;

public sealed class CancelarPedidoTests
{
    private sealed class FakeRepositorio : IPedidoRepository
    {
        private readonly Pedido? _pedido;

        public FakeRepositorio(Pedido? pedido) => _pedido = pedido;

        public int Salvamentos { get; private set; }

        public Task<Pedido?> ObterPorIdAsync(Guid id, CancellationToken ct) => Task.FromResult(_pedido);
        public Task<Pedido?> ObterParaEntregadorAsync(Guid id, CancellationToken ct) => Task.FromResult(_pedido);
        public Task<Pedido?> ObterEntregaEmCursoAsync(Guid entregadorId, CancellationToken ct) =>
            Task.FromResult<Pedido?>(null);

        public Task<bool> TemEntregaAtivaAsync(Guid entregadorId, Guid merchantId, CancellationToken ct) =>
            Task.FromResult(false);
        public void Adicionar(Pedido pedido) => throw new NotSupportedException("CancelarPedido não cria pedido.");

        public Task SalvarAsync(CancellationToken ct)
        {
            Salvamentos++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOrigem : IOrderSource
    {
        private readonly Result _resposta;

        public FakeOrigem(Result resposta) => _resposta = resposta;

        public List<string> Chamadas { get; } = [];

        public Task<Result> ConfirmarPedidoAsync(string id, CancellationToken ct) => Task.FromResult(Result.Success());
        public Task<Result> IniciarPreparoAsync(string id, CancellationToken ct) => Task.FromResult(Result.Success());
        public Task<Result> MarcarProntoAsync(string id, CancellationToken ct) => Task.FromResult(Result.Success());
        public Task<Result> DespacharAsync(string id, CancellationToken ct) => Task.FromResult(Result.Success());

        public Task<Result> CancelarPedidoAsync(string id, string motivo, CancellationToken ct)
        {
            Chamadas.Add($"cancelar:{motivo}");
            return Task.FromResult(_resposta);
        }

        public Task<Result<bool>> VerificarCodigoDeEntregaAsync(string id, string codigo, CancellationToken ct) =>
            Task.FromResult(Result.Success(true));
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

    private static Pedido NovoPedido(string idExterno) => Pedido.Receber(
        merchantId: Guid.CreateVersion7(),
        idExterno: idExterno,
        numeroExibicao: "1578",
        ehTeste: true,
        cliente: new Cliente("Cliente de Teste", null, null),
        enderecoEntrega: null,
        valorTotal: 27m,
        taxaEntrega: 0m,
        criadoNaOrigemEm: DateTimeOffset.UtcNow,
        recebidoEm: DateTimeOffset.UtcNow);

    private static CancelarPedido Criar(IPedidoRepository repo, FakeOrigem origem, FakeNotificador notificador) =>
        new(repo, origem, notificador, TimeProvider.System);

    [Fact]
    public async Task Motivo_vazio_e_rejeitado_sem_tocar_origem_nem_salvar()
    {
        var repo = new FakeRepositorio(NovoPedido("b57177eb-158b-4308-92ca-56aaaecad387"));
        var origem = new FakeOrigem(Result.Success());

        var resultado = await Criar(repo, origem, new FakeNotificador())
            .ExecutarAsync(Guid.CreateVersion7(), "   ", CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(PedidoErrors.MotivoCancelamentoObrigatorio, resultado.Error);
        Assert.Empty(origem.Chamadas);
        Assert.Equal(0, repo.Salvamentos);
    }

    [Fact]
    public async Task Pedido_inexistente_devolve_nao_encontrado()
    {
        var origem = new FakeOrigem(Result.Success());

        var resultado = await Criar(new FakeRepositorio(null), origem, new FakeNotificador())
            .ExecutarAsync(Guid.CreateVersion7(), "Item em falta", CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(AvancarPedidoErrors.PedidoNaoEncontrado, resultado.Error);
        Assert.Empty(origem.Chamadas);
    }

    [Fact]
    public async Task Pedido_externo_avisa_a_origem_cancela_e_notifica()
    {
        var pedido = NovoPedido("b57177eb-158b-4308-92ca-56aaaecad387");
        var repo = new FakeRepositorio(pedido);
        var origem = new FakeOrigem(Result.Success());
        var notificador = new FakeNotificador();

        var resultado = await Criar(repo, origem, notificador)
            .ExecutarAsync(pedido.Id, "Loja fechando", CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Contains("cancelar:Loja fechando", origem.Chamadas);
        Assert.Equal(StatusPedido.Cancelado, pedido.Status);
        Assert.Equal("Loja fechando", pedido.MotivoCancelamento);
        Assert.Equal(1, repo.Salvamentos);
        Assert.Contains(pedido.MerchantId, notificador.Avisados);
    }

    [Fact]
    public async Task Pedido_interno_nao_avisa_a_origem()
    {
        var pedido = NovoPedido($"{Pedido.PrefixoOrigemLocal}42");
        var repo = new FakeRepositorio(pedido);
        var origem = new FakeOrigem(Result.Success());

        var resultado = await Criar(repo, origem, new FakeNotificador())
            .ExecutarAsync(pedido.Id, "Cliente desistiu", CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Empty(origem.Chamadas);
        Assert.Equal(StatusPedido.Cancelado, pedido.Status);
        Assert.Equal(1, repo.Salvamentos);
    }

    [Fact]
    public async Task Origem_recusando_nao_cancela_local_nem_salva()
    {
        var pedido = NovoPedido("b57177eb-158b-4308-92ca-56aaaecad387");
        var repo = new FakeRepositorio(pedido);
        var origem = new FakeOrigem(Result.Failure(new Error("origem.recusou", "Recusou", ErrorType.Conflict)));

        var resultado = await Criar(repo, origem, new FakeNotificador())
            .ExecutarAsync(pedido.Id, "Item em falta", CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.NotEqual(StatusPedido.Cancelado, pedido.Status);
        Assert.Equal(0, repo.Salvamentos);
    }
}
