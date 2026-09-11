using DeliveryHub.Domain.Orders;

namespace DeliveryHub.Domain.Tests.Orders;

public sealed class PedidoTests
{
    private static Pedido Novo() =>
        Pedido.Receber(Guid.CreateVersion7(), "b57177eb-158b-4308-92ca-56aaaecad387", ehTeste: false, DateTimeOffset.UtcNow);

    [Fact]
    public void Pedido_nasce_em_recebido()
    {
        Assert.Equal(StatusPedido.Recebido, Novo().Status);
    }

    [Fact]
    public void Percorre_o_ciclo_completo_do_ifood()
    {
        var pedido = Novo();

        Assert.True(pedido.Confirmar().IsSuccess);
        Assert.True(pedido.IniciarPreparo().IsSuccess);
        Assert.True(pedido.MarcarPronto().IsSuccess);
        Assert.True(pedido.Despachar().IsSuccess);
        Assert.True(pedido.Concluir().IsSuccess);

        Assert.Equal(StatusPedido.Concluido, pedido.Status);
    }

    [Fact]
    public void Confirmar_pedido_ja_confirmado_e_sucesso_silencioso()
    {
        // Idempotência nível 3: o iFood reenvia evento não reconhecido, então
        // confirmar duas vezes não pode virar erro.
        var pedido = Novo();
        pedido.Confirmar();

        var segunda = pedido.Confirmar();

        Assert.True(segunda.IsSuccess);
        Assert.Equal(StatusPedido.Confirmado, pedido.Status);
    }

    [Fact]
    public void Evento_atrasado_nao_retrocede_o_status()
    {
        // O polling não garante ordem: um CFM atrasado chegando depois de DSP
        // não pode desfazer o despacho.
        var pedido = Novo();
        pedido.Confirmar();
        pedido.Despachar();

        var atrasado = pedido.IniciarPreparo();

        Assert.True(atrasado.IsSuccess);
        Assert.Equal(StatusPedido.Despachado, pedido.Status);
    }

    [Fact]
    public void Pode_pular_estados_opcionais()
    {
        // PREPARATION_STARTED e READY_TO_PICKUP são opcionais na doc do iFood:
        // o pedido pode ir de confirmado direto para despachado.
        var pedido = Novo();
        pedido.Confirmar();

        Assert.True(pedido.Despachar().IsSuccess);
        Assert.Equal(StatusPedido.Despachado, pedido.Status);
    }

    [Theory]
    [InlineData(StatusPedido.Recebido)]
    [InlineData(StatusPedido.Confirmado)]
    [InlineData(StatusPedido.EmPreparo)]
    [InlineData(StatusPedido.Pronto)]
    [InlineData(StatusPedido.Despachado)]
    public void Cancela_de_qualquer_estado_nao_terminal(StatusPedido ate)
    {
        var pedido = Novo();
        if (ate >= StatusPedido.Confirmado) pedido.Confirmar();
        if (ate >= StatusPedido.EmPreparo) pedido.IniciarPreparo();
        if (ate >= StatusPedido.Pronto) pedido.MarcarPronto();
        if (ate >= StatusPedido.Despachado) pedido.Despachar();

        Assert.True(pedido.Cancelar().IsSuccess);
        Assert.Equal(StatusPedido.Cancelado, pedido.Status);
    }

    [Fact]
    public void Pedido_concluido_nao_cancela()
    {
        var pedido = Novo();
        pedido.Confirmar();
        pedido.Concluir();

        var resultado = pedido.Cancelar();

        Assert.True(resultado.IsFailure);
        Assert.Equal(PedidoErrors.PedidoConcluido, resultado.Error);
        Assert.Equal(StatusPedido.Concluido, pedido.Status);
    }

    [Fact]
    public void Pedido_cancelado_nao_avanca()
    {
        var pedido = Novo();
        pedido.Cancelar();

        var resultado = pedido.Confirmar();

        Assert.True(resultado.IsFailure);
        Assert.Equal(PedidoErrors.PedidoCancelado, resultado.Error);
        Assert.Equal(StatusPedido.Cancelado, pedido.Status);
    }

    [Fact]
    public void Cancelar_duas_vezes_e_sucesso_silencioso()
    {
        var pedido = Novo();
        pedido.Cancelar();

        Assert.True(pedido.Cancelar().IsSuccess);
    }

    [Fact]
    public void Transicao_invalida_devolve_Result_e_nao_lanca_excecao()
    {
        // ENGINEERING-GUIDE §3: falha de negócio é Result, exceção é para o
        // inesperado. Transição inválida é fluxo previsto.
        var pedido = Novo();
        pedido.Concluir();

        var excecao = Record.Exception(() => pedido.Confirmar());

        Assert.Null(excecao);
    }

    [Fact]
    public void Pedido_de_teste_carrega_a_marcacao_ate_o_dominio()
    {
        // isTest vem do payload real do iFood; sem isso, pedido de sandbox
        // vira lançamento financeiro de verdade quando o Ledger existir.
        var pedido = Pedido.Receber(Guid.CreateVersion7(), "abc", ehTeste: true, DateTimeOffset.UtcNow);

        Assert.True(pedido.EhTeste);
    }
}
