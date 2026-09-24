using DeliveryHub.Domain.Orders;

namespace DeliveryHub.Domain.Tests.Orders;

public sealed class PedidoTests
{
    private static Pedido Novo(bool ehTeste = false, Pagamento? pagamento = null) => Pedido.Receber(
        merchantId: Guid.CreateVersion7(),
        idExterno: "b57177eb-158b-4308-92ca-56aaaecad387",
        numeroExibicao: "1578",
        ehTeste: ehTeste,
        cliente: new Cliente("Cliente de Teste", "0800 000 0000", "000000"),
        enderecoEntrega: null,
        valorTotal: 50m,
        taxaEntrega: 5m,
        criadoNaOrigemEm: DateTimeOffset.UtcNow,
        recebidoEm: DateTimeOffset.UtcNow,
        pagamento: pagamento);

    // Leva o pedido até "Chegou": é de lá que sai o passo de cobrar.
    private static Pedido NoLocal(Pagamento pagamento)
    {
        var pedido = Novo(pagamento: pagamento);
        pedido.Confirmar();
        pedido.AlocarEntregador(Guid.CreateVersion7());
        pedido.Despachar();
        pedido.AceitarEntrega();
        pedido.SairParaEntrega();
        pedido.ChegarNoLocal();

        return pedido;
    }

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
        Assert.True(pedido.AlocarEntregador(Guid.CreateVersion7()).IsSuccess);
        Assert.True(pedido.Despachar().IsSuccess);
        Assert.True(pedido.Concluir().IsSuccess);

        Assert.Equal(StatusPedido.Concluido, pedido.Status);
    }

    [Fact]
    public void Percorre_o_ciclo_da_entrega_apos_o_despacho()
    {
        // As transições do motoboy, uma por uma — não são atalho de
        // AvancarPara pulando estado, são passo real da entrega.
        var pedido = Novo();
        pedido.Confirmar();
        pedido.MarcarPronto();
        pedido.AlocarEntregador(Guid.CreateVersion7());
        pedido.Despachar();

        Assert.True(pedido.AceitarEntrega().IsSuccess);
        Assert.Equal(StatusPedido.Aceito, pedido.Status);

        Assert.True(pedido.SairParaEntrega().IsSuccess);
        Assert.Equal(StatusPedido.EmRota, pedido.Status);

        Assert.True(pedido.ChegarNoLocal().IsSuccess);
        Assert.Equal(StatusPedido.Chegou, pedido.Status);

        Assert.True(pedido.Concluir().IsSuccess);
        Assert.Equal(StatusPedido.Concluido, pedido.Status);
    }

    [Fact]
    public void Alocar_entregador_define_o_entregador_sem_mudar_o_status()
    {
        var pedido = Novo();
        pedido.Confirmar();
        pedido.MarcarPronto();
        var entregadorId = Guid.CreateVersion7();

        var resultado = pedido.AlocarEntregador(entregadorId);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(entregadorId, pedido.EntregadorId);
        Assert.Equal(StatusPedido.Pronto, pedido.Status);
    }

    [Fact]
    public void Despachar_sem_entregador_alocado_falha()
    {
        var pedido = Novo();
        pedido.Confirmar();
        pedido.MarcarPronto();

        var resultado = pedido.Despachar();

        Assert.True(resultado.IsFailure);
        Assert.Equal(PedidoErrors.SemEntregadorAlocado, resultado.Error);
        Assert.Equal(StatusPedido.Pronto, pedido.Status);
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
        pedido.AlocarEntregador(Guid.CreateVersion7());
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
        pedido.AlocarEntregador(Guid.CreateVersion7());

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
        if (ate >= StatusPedido.Despachado) pedido.AlocarEntregador(Guid.CreateVersion7());
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
    public void Cancelar_guarda_motivo_e_data()
    {
        var pedido = Novo();
        var quando = DateTimeOffset.Parse("2026-09-23T12:00:00Z");

        pedido.Cancelar("Item em falta", quando);

        Assert.Equal(StatusPedido.Cancelado, pedido.Status);
        Assert.Equal("Item em falta", pedido.MotivoCancelamento);
        Assert.Equal(quando, pedido.CanceladoEm);
    }

    [Fact]
    public void Cancelar_de_novo_nao_reescreve_motivo_nem_data()
    {
        var pedido = Novo();
        var primeira = DateTimeOffset.Parse("2026-09-23T12:00:00Z");
        pedido.Cancelar("Primeiro motivo", primeira);

        pedido.Cancelar("Segundo motivo", primeira.AddHours(1));

        Assert.Equal("Primeiro motivo", pedido.MotivoCancelamento);
        Assert.Equal(primeira, pedido.CanceladoEm);
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
    public void Cobrar_quita_a_pendencia_e_move_o_status()
    {
        var pedido = NoLocal(new Pagamento(ValorJaPago: 0, ValorACobrar: 55m, "Dinheiro"));

        var resultado = pedido.Cobrar();

        Assert.True(resultado.IsSuccess);
        Assert.Equal(StatusPedido.Cobrar, pedido.Status);
        Assert.Equal(55m, pedido.Pagamento.ValorJaPago);
        Assert.Equal(0m, pedido.Pagamento.ValorACobrar);
        Assert.False(pedido.Pagamento.PrecisaCobrarNaEntrega);
    }

    [Fact]
    public void Cobrar_soma_ao_que_ja_havia_sido_pago_online()
    {
        // O iFood manda pedido parcialmente pago: parte no cartão, o troco em
        // dinheiro na porta. Cobrar fecha só a diferença, não o total.
        var pedido = NoLocal(new Pagamento(ValorJaPago: 35m, ValorACobrar: 20m, "Crédito · Visa"));

        pedido.Cobrar();

        Assert.Equal(55m, pedido.Pagamento.ValorJaPago);
        Assert.Equal(0m, pedido.Pagamento.ValorACobrar);
    }

    [Fact]
    public void Cobrar_pedido_pago_online_falha_sem_mover_o_status()
    {
        // Pedido pago no app não pode ganhar o passo de cobrar: seria pedir
        // dinheiro duas vezes ao cliente.
        var pedido = NoLocal(new Pagamento(ValorJaPago: 55m, ValorACobrar: 0m, "Pix"));

        var resultado = pedido.Cobrar();

        Assert.True(resultado.IsFailure);
        Assert.Equal(PedidoErrors.PedidoJaPago, resultado.Error);
        Assert.Equal(StatusPedido.Chegou, pedido.Status);
    }

    [Fact]
    public void Cobrar_pedido_sem_informacao_de_pagamento_falha()
    {
        // Pagamento.Indefinido vale como pago: sem dado, o sistema não inventa
        // cobrança que o motoboy repassaria como se fosse real.
        var pedido = NoLocal(Pagamento.Indefinido);

        Assert.True(pedido.Cobrar().IsFailure);
    }

    [Fact]
    public void Cobrar_duas_vezes_nao_credita_o_valor_de_novo()
    {
        // Duplo clique no botão do motoboy, ou retry de rede. A segunda chamada
        // recusa porque não há mais pendência — e o importante é que o valor
        // recebido não dobra.
        var pedido = NoLocal(new Pagamento(ValorJaPago: 0, ValorACobrar: 55m, "Dinheiro"));
        pedido.Cobrar();

        var segunda = pedido.Cobrar();

        Assert.True(segunda.IsFailure);
        Assert.Equal(55m, pedido.Pagamento.ValorJaPago);
        Assert.Equal(StatusPedido.Cobrar, pedido.Status);
    }

    [Fact]
    public void Pedido_a_cobrar_so_entra_na_receita_depois_de_cobrado_e_concluido()
    {
        // A regra da receita lê estas duas coisas juntas: status Concluido e
        // nada a cobrar. Enquanto o motoboy não recebeu, o dinheiro não é do
        // caixa — só vira receita quando as duas condições valem.
        var pedido = NoLocal(new Pagamento(ValorJaPago: 0, ValorACobrar: 55m, "Dinheiro"));

        pedido.Concluir();
        Assert.True(pedido.Pagamento.PrecisaCobrarNaEntrega);

        // Concluir não pula a cobrança: o valor continua pendente depois dele.
        Assert.Equal(0m, pedido.Pagamento.ValorJaPago);
    }

    [Fact]
    public void Pedido_de_teste_carrega_a_marcacao_ate_o_dominio()
    {
        // isTest vem do payload real do iFood; sem isso, pedido de sandbox
        // vira lançamento financeiro de verdade quando o Ledger existir.
        Assert.True(Novo(ehTeste: true).EhTeste);
    }
}
