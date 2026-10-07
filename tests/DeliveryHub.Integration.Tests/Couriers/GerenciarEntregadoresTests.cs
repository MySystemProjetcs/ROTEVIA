using DeliveryHub.Application.Abstractions;
using DeliveryHub.Application.Couriers;
using DeliveryHub.Domain.Couriers;
using DeliveryHub.Domain.Orders;

namespace DeliveryHub.Integration.Tests.Couriers;

public sealed class GerenciarEntregadoresTests
{
    private static readonly Guid MerchantId = Guid.CreateVersion7();
    private static readonly Guid OutroMerchantId = Guid.CreateVersion7();

    private sealed class FakeCouriers : ICourierRepository
    {
        private readonly Courier? _courier;
        private readonly CourierMerchantLink? _link;
        public FakeCouriers(Courier? courier, CourierMerchantLink? link)
        {
            _courier = courier;
            _link = link;
        }

        public CourierMerchantLink? Removido { get; private set; }
        public int Salvamentos { get; private set; }

        public Task<Courier?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(_courier?.Id == id ? _courier : null);
        public Task<CourierMerchantLink?> ObterLinkAsync(Guid linkId, CancellationToken ct) =>
            Task.FromResult(_link?.Id == linkId ? _link : null);
        public void RemoverLink(CourierMerchantLink link) => Removido = link;
        public Task SalvarAsync(CancellationToken ct)
        {
            Salvamentos++;
            return Task.CompletedTask;
        }

        public Task<Courier?> ObterPorCpfAsync(string cpf, CancellationToken ct) => throw new NotSupportedException();
        public Task<Courier?> ObterPorUsuarioIdAsync(Guid usuarioId, CancellationToken ct) => throw new NotSupportedException();
        public void Adicionar(Courier courier) => throw new NotSupportedException();
        public Task<bool> ExisteVinculoAtivoOuPendenteAsync(Guid courierId, Guid merchantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ExisteVinculoAtivoAsync(Guid courierId, Guid merchantId, CancellationToken ct) => throw new NotSupportedException();
        public void AdicionarLink(CourierMerchantLink link) => throw new NotSupportedException();
        public Task<IReadOnlyList<EntregadorResumo>> ListarPorMerchantAsync(Guid merchantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> ListarMerchantIdsAtivosAsync(Guid courierId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakePedidos : IPedidoRepository
    {
        private readonly bool _temEntregaAtiva;
        public FakePedidos(bool temEntregaAtiva) => _temEntregaAtiva = temEntregaAtiva;

        public Task<bool> TemEntregaAtivaAsync(Guid entregadorId, Guid merchantId, CancellationToken ct) =>
            Task.FromResult(_temEntregaAtiva);

        public Task<Pedido?> ObterPorIdAsync(Guid id, CancellationToken ct) => Task.FromResult<Pedido?>(null);
        public Task<Pedido?> ObterParaEntregadorAsync(Guid id, CancellationToken ct) => Task.FromResult<Pedido?>(null);
        public Task<Pedido?> ObterEntregaEmCursoAsync(Guid entregadorId, CancellationToken ct) => Task.FromResult<Pedido?>(null);
        public void Adicionar(Pedido pedido) => throw new NotSupportedException();
        public Task SalvarAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private static Courier NovoCourier() =>
        Courier.Criar("12345678901", "João", "11999998888", "Honda CG", "ABC1D23", DateTimeOffset.UtcNow);

    private static CourierMerchantLink ConviteParaCourier(Courier courier, Guid merchantId)
    {
        var agora = DateTimeOffset.UtcNow;
        return CourierMerchantLink.Convidar(
            courier.Id, merchantId, "joao@teste.local", "hash", agora.AddDays(1), agora);
    }

    private static CourierMerchantLink VinculoAtivo(Courier courier, Guid merchantId)
    {
        var link = ConviteParaCourier(courier, merchantId);
        link.Ativar(DateTimeOffset.UtcNow);
        return link;
    }

    // ---------- Remover vínculo / revogar convite ----------

    [Fact]
    public async Task Revoga_convite_pendente_apagando_o_vinculo()
    {
        var courier = NovoCourier();
        var convite = ConviteParaCourier(courier, MerchantId);
        var couriers = new FakeCouriers(courier, convite);

        var resultado = await new RemoverVinculoDoEntregador(couriers, new FakePedidos(false))
            .ExecutarAsync(MerchantId, convite.Id, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Same(convite, couriers.Removido);
        Assert.Equal(1, couriers.Salvamentos);
    }

    [Fact]
    public async Task Desvincula_entregador_ativo_sem_entrega_em_andamento()
    {
        var courier = NovoCourier();
        var link = VinculoAtivo(courier, MerchantId);
        var couriers = new FakeCouriers(courier, link);

        var resultado = await new RemoverVinculoDoEntregador(couriers, new FakePedidos(false))
            .ExecutarAsync(MerchantId, link.Id, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Same(link, couriers.Removido);
    }

    [Fact]
    public async Task Entrega_em_andamento_impede_a_remocao()
    {
        var courier = NovoCourier();
        var link = VinculoAtivo(courier, MerchantId);
        var couriers = new FakeCouriers(courier, link);

        var resultado = await new RemoverVinculoDoEntregador(couriers, new FakePedidos(true))
            .ExecutarAsync(MerchantId, link.Id, CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(CourierErrors.EntregadorComEntregaAtiva, resultado.Error);
        Assert.Null(couriers.Removido);
    }

    [Fact]
    public async Task Convite_pendente_nao_e_barrado_por_entrega_ativa()
    {
        // Convite ainda não aceito não tem entrega atrelada: a guarda não deve
        // valer aqui, senão um convite antigo ficaria impossível de revogar.
        var courier = NovoCourier();
        var convite = ConviteParaCourier(courier, MerchantId);
        var couriers = new FakeCouriers(courier, convite);

        var resultado = await new RemoverVinculoDoEntregador(couriers, new FakePedidos(true))
            .ExecutarAsync(MerchantId, convite.Id, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Same(convite, couriers.Removido);
    }

    [Fact]
    public async Task Loja_nao_remove_vinculo_de_outra_loja()
    {
        var courier = NovoCourier();
        var link = VinculoAtivo(courier, OutroMerchantId);
        var couriers = new FakeCouriers(courier, link);

        var resultado = await new RemoverVinculoDoEntregador(couriers, new FakePedidos(false))
            .ExecutarAsync(MerchantId, link.Id, CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(CourierErrors.VinculoNaoEncontrado, resultado.Error);
        Assert.Null(couriers.Removido);
    }

    // ---------- Atualizar cadastro ----------

    [Fact]
    public async Task Atualiza_cadastro_do_entregador()
    {
        var courier = NovoCourier();
        var link = VinculoAtivo(courier, MerchantId);
        var couriers = new FakeCouriers(courier, link);

        var resultado = await new AtualizarCadastroDoEntregador(couriers).ExecutarAsync(
            MerchantId, link.Id, "João da Silva", "11911112222", "Yamaha Factor", "xyz9k88",
            CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("João da Silva", courier.Nome);
        Assert.Equal("11911112222", courier.Telefone);
        Assert.Equal("Yamaha Factor", courier.ModeloDaMoto);
        // Placa normalizada em caixa alta.
        Assert.Equal("XYZ9K88", courier.Placa);
        // CPF é chave natural: não muda.
        Assert.Equal("12345678901", courier.Cpf);
        Assert.Equal(1, couriers.Salvamentos);
    }

    [Fact]
    public async Task Campo_vazio_e_rejeitado()
    {
        var courier = NovoCourier();
        var link = VinculoAtivo(courier, MerchantId);
        var couriers = new FakeCouriers(courier, link);

        var resultado = await new AtualizarCadastroDoEntregador(couriers).ExecutarAsync(
            MerchantId, link.Id, "  ", "11911112222", "Yamaha", "XYZ9K88", CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(CourierErrors.DadosInvalidos, resultado.Error);
        Assert.Equal("João", courier.Nome);
        Assert.Equal(0, couriers.Salvamentos);
    }

    [Fact]
    public async Task Loja_nao_edita_entregador_de_outra_loja()
    {
        var courier = NovoCourier();
        var link = VinculoAtivo(courier, OutroMerchantId);
        var couriers = new FakeCouriers(courier, link);

        var resultado = await new AtualizarCadastroDoEntregador(couriers).ExecutarAsync(
            MerchantId, link.Id, "Invasor", "11900000000", "Moto", "AAA0A00", CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(CourierErrors.VinculoNaoEncontrado, resultado.Error);
        Assert.Equal("João", courier.Nome);
    }
}
