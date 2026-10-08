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

        public Task<Pedido?> ObterParaEntregadorAsync(Guid id, CancellationToken ct) => Task.FromResult(_pedido);

        public Task<Pedido?> ObterEntregaEmCursoAsync(Guid entregadorId, CancellationToken ct) =>
            Task.FromResult<Pedido?>(null);

        public Task<bool> TemEntregaAtivaAsync(Guid entregadorId, Guid merchantId, CancellationToken ct) =>
            Task.FromResult(false);

        public void Adicionar(Pedido pedido) => throw new NotSupportedException("AvancarPedido não cria pedido.");

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
        public Task<Result> CancelarPedidoAsync(string id, string motivo, CancellationToken ct) => Registrar("cancelar");

        // O AvancarPedido não valida código — só o fluxo do entregador faz
        // isso. Aqui o método existe para satisfazer a porta.
        public Task<Result<bool>> VerificarCodigoDeEntregaAsync(
            string id, string codigo, CancellationToken ct)
        {
            Chamadas.Add("verificar-codigo");
            return Task.FromResult(Result.Success(true));
        }
        public Task<Result<bool>> ValidarCodigoDeColetaAsync(
            string id, string codigo, CancellationToken ct)
        {
            Chamadas.Add("validar-coleta");
            return Task.FromResult(Result.Success(true));
        }
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

        var resultado = await new AvancarPedido(new FakeRepositorio(null), origem, new FakeNotificador())
            .ExecutarAsync(Guid.CreateVersion7(), AcaoDePedido.Confirmar, CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(AvancarPedidoErrors.PedidoNaoEncontrado, resultado.Error);
        Assert.Empty(origem.Chamadas);
    }

    [Theory]
    [InlineData(AcaoDePedido.Confirmar, "confirmar", StatusPedido.Confirmado)]
    [InlineData(AcaoDePedido.IniciarPreparo, "iniciar-preparo", StatusPedido.EmPreparo)]
    [InlineData(AcaoDePedido.MarcarPronto, "pronto", StatusPedido.Pronto)]
    public async Task Cada_acao_avisa_a_origem_certa_e_move_o_pedido(
        AcaoDePedido acao, string chamadaEsperada, StatusPedido statusEsperado)
    {
        var pedido = NovoPedido();
        var origem = new FakeOrigem(Result.Success());

        await new AvancarPedido(new FakeRepositorio(pedido), origem, new FakeNotificador())
            .ExecutarAsync(pedido.Id, acao, CancellationToken.None);

        Assert.Equal([chamadaEsperada], origem.Chamadas);
        Assert.Equal(statusEsperado, pedido.Status);
    }

    [Fact]
    public async Task Pedido_de_origem_local_nao_chama_a_origem_mas_avanca()
    {
        // Venda nascida dentro do sistema (PDV próprio, gerador de validação)
        // não existe no marketplace: chamar a API de lá devolveria recusa.
        var pedido = Pedido.Receber(
            merchantId: Guid.CreateVersion7(),
            idExterno: $"{Pedido.PrefixoOrigemLocal}{Guid.CreateVersion7()}",
            numeroExibicao: "2280",
            ehTeste: false,
            cliente: new Cliente("Cliente Local", null, null),
            enderecoEntrega: null,
            valorTotal: 74.90m,
            taxaEntrega: 7.90m,
            criadoNaOrigemEm: DateTimeOffset.UtcNow,
            recebidoEm: DateTimeOffset.UtcNow);

        var repositorio = new FakeRepositorio(pedido);
        var origem = new FakeOrigem(Result.Success());

        var resultado = await new AvancarPedido(repositorio, origem, new FakeNotificador())
            .ExecutarAsync(pedido.Id, AcaoDePedido.Confirmar, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Empty(origem.Chamadas);
        Assert.Equal(StatusPedido.Confirmado, pedido.Status);
        Assert.Equal(1, repositorio.Salvamentos);
    }

    [Fact]
    public async Task Despachar_com_entregador_ja_alocado_avisa_a_origem_e_move_o_pedido()
    {
        var pedido = NovoPedido();
        pedido.AlocarEntregador(Guid.CreateVersion7());
        var origem = new FakeOrigem(Result.Success());

        await new AvancarPedido(new FakeRepositorio(pedido), origem, new FakeNotificador())
            .ExecutarAsync(pedido.Id, AcaoDePedido.Despachar, CancellationToken.None);

        Assert.Equal(["despachar"], origem.Chamadas);
        Assert.Equal(StatusPedido.Despachado, pedido.Status);
    }

    [Fact]
    public async Task Despachar_sem_entregador_alocado_nao_avisa_a_origem_nem_persiste()
    {
        // A pré-condição é checada antes de notificar o iFood: sem isso o
        // marketplace saberia que o pedido "saiu" mesmo sem motoboy nenhum.
        var pedido = NovoPedido();
        var repositorio = new FakeRepositorio(pedido);
        var origem = new FakeOrigem(Result.Success());

        var resultado = await new AvancarPedido(repositorio, origem, new FakeNotificador())
            .ExecutarAsync(pedido.Id, AcaoDePedido.Despachar, CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(PedidoErrors.SemEntregadorAlocado, resultado.Error);
        Assert.Empty(origem.Chamadas);
        Assert.Equal(0, repositorio.Salvamentos);
    }

    [Fact]
    public async Task Avisa_a_origem_antes_de_mudar_o_status_local()
    {
        // Se o status mudasse primeiro, o lojista veria um estado que o
        // marketplace ainda não reconhece — e que pode ser recusado.
        var pedido = NovoPedido();
        var origem = new FakeOrigem(Result.Success()) { Observado = pedido };

        await new AvancarPedido(new FakeRepositorio(pedido), origem, new FakeNotificador())
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

        var resultado = await new AvancarPedido(repositorio, new FakeOrigem(recusa), new FakeNotificador())
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

        var resultado = await new AvancarPedido(repositorio, new FakeOrigem(Result.Success()), new FakeNotificador())
            .ExecutarAsync(pedido.Id, AcaoDePedido.Confirmar, CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(PedidoErrors.PedidoCancelado, resultado.Error);
        Assert.Equal(0, repositorio.Salvamentos);
    }
}
