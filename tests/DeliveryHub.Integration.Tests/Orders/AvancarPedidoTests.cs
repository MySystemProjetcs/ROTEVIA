using DeliveryHub.Application.Abstractions;
using DeliveryHub.Application.Orders;
using DeliveryHub.Domain.Orders;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Integration.Tests.Orders;

public sealed class AvancarPedidoTests
{
    private sealed class FakeRepositorio : IPedidoRepository
    {
        private readonly Pedido? _pedido;

        public FakeRepositorio(Pedido? pedido) => _pedido = pedido;

        public int Salvamentos { get; private set; }

        public Task<Pedido?> ObterPorIdAsync(Guid id, CancellationToken ct) => Task.FromResult(_pedido);

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
        public StatusPedido? StatusQuandoChamada { get; private set; }
        public Pedido? Observado { get; set; }

        private Task<Result> Registrar(string acao)
        {
            Chamadas.Add(acao);
            StatusQuandoChamada = Observado?.Status;
            return Task.FromResult(_resposta);
        }

        public Task<Result> ConfirmarPedidoAsync(string id, CancellationToken ct) => Registrar("confirmar");
        public Task<Result> IniciarPreparoAsync(string id, CancellationToken ct) => Registrar("iniciar-preparo");
        public Task<Result> MarcarProntoAsync(string id, CancellationToken ct) => Registrar("pronto");
        public Task<Result> DespacharAsync(string id, CancellationToken ct) => Registrar("despachar");
    }

    private static Pedido NovoPedido() => Pedido.Receber(
        merchantId: Guid.CreateVersion7(),
        idExterno: "b57177eb-158b-4308-92ca-56aaaecad387",
        numeroExibicao: "1578",
        ehTeste: true,
        cliente: new Cliente("Cliente de Teste", null, null),
        enderecoEntrega: null,
        valorTotal: 27m,
        taxaEntrega: 0m,
        criadoNaOrigemEm: DateTimeOffset.UtcNow,
        recebidoEm: DateTimeOffset.UtcNow);

    [Fact]
    public async Task Pedido_inexistente_nao_chama_a_origem()
    {
        var origem = new FakeOrigem(Result.Success());

        var resultado = await new AvancarPedido(new FakeRepositorio(null), origem)
            .ExecutarAsync(Guid.CreateVersion7(), AcaoDePedido.Confirmar, CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(AvancarPedidoErrors.PedidoNaoEncontrado, resultado.Error);
        Assert.Empty(origem.Chamadas);
    }

    [Theory]
    [InlineData(AcaoDePedido.Confirmar, "confirmar", StatusPedido.Confirmado)]
    [InlineData(AcaoDePedido.IniciarPreparo, "iniciar-preparo", StatusPedido.EmPreparo)]
    [InlineData(AcaoDePedido.MarcarPronto, "pronto", StatusPedido.Pronto)]
    [InlineData(AcaoDePedido.Despachar, "despachar", StatusPedido.Despachado)]
    public async Task Cada_acao_avisa_a_origem_certa_e_move_o_pedido(
        AcaoDePedido acao, string chamadaEsperada, StatusPedido statusEsperado)
    {
        var pedido = NovoPedido();
        var origem = new FakeOrigem(Result.Success());

        await new AvancarPedido(new FakeRepositorio(pedido), origem)
            .ExecutarAsync(pedido.Id, acao, CancellationToken.None);

        Assert.Equal([chamadaEsperada], origem.Chamadas);
        Assert.Equal(statusEsperado, pedido.Status);
    }

    [Fact]
    public async Task Avisa_a_origem_antes_de_mudar_o_status_local()
    {
        // Se o status mudasse primeiro, o lojista veria um estado que o
        // marketplace ainda não reconhece — e que pode ser recusado.
        var pedido = NovoPedido();
        var origem = new FakeOrigem(Result.Success()) { Observado = pedido };

        await new AvancarPedido(new FakeRepositorio(pedido), origem)
            .ExecutarAsync(pedido.Id, AcaoDePedido.Confirmar, CancellationToken.None);

        Assert.Equal(StatusPedido.Recebido, origem.StatusQuandoChamada);
        Assert.Equal(StatusPedido.Confirmado, pedido.Status);
    }

    [Fact]
    public async Task Origem_recusando_nao_muda_status_nem_salva()
    {
        var pedido = NovoPedido();
        var repositorio = new FakeRepositorio(pedido);
        var recusa = Result.Failure(new Error("origem.recusou", "Recusado.", ErrorType.Conflict));

        var resultado = await new AvancarPedido(repositorio, new FakeOrigem(recusa))
            .ExecutarAsync(pedido.Id, AcaoDePedido.Confirmar, CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(StatusPedido.Recebido, pedido.Status);
        Assert.Equal(0, repositorio.Salvamentos);
    }

    [Fact]
    public async Task Pedido_cancelado_nao_avanca_e_nao_persiste()
    {
        var pedido = NovoPedido();
        pedido.Cancelar();
        var repositorio = new FakeRepositorio(pedido);

        var resultado = await new AvancarPedido(repositorio, new FakeOrigem(Result.Success()))
            .ExecutarAsync(pedido.Id, AcaoDePedido.Confirmar, CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(PedidoErrors.PedidoCancelado, resultado.Error);
        Assert.Equal(0, repositorio.Salvamentos);
    }
}
