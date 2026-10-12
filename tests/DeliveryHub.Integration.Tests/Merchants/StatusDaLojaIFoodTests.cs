using DeliveryHub.Api.Merchants;

namespace DeliveryHub.Integration.Tests.Merchants;

// O painel pinta "Online" a partir deste resultado. Um falso positivo aqui faz
// o lojista acreditar que está recebendo pedido enquanto a loja está fechada
// no iFood — por isso cada ramo tem caso, inclusive os de payload torto.
public sealed class StatusDaLojaIFoodTests
{
    [Fact]
    public void Disponibilidade_deve_ser_online_quando_entrega_no_canal_ifood_esta_disponivel()
    {
        var json = """
        [
          { "operation": "DELIVERY", "salesChannel": "IFOOD", "available": true },
          { "operation": "TAKEOUT",  "salesChannel": "IFOOD", "available": false }
        ]
        """;

        Assert.Equal(StatusDaLojaIFood.Online, StatusDaLojaIFood.Disponibilidade(json));
    }

    [Fact]
    public void Disponibilidade_deve_ser_offline_quando_entrega_no_canal_ifood_esta_indisponivel()
    {
        var json = """
        [{ "operation": "DELIVERY", "salesChannel": "IFOOD", "available": false }]
        """;

        Assert.Equal(StatusDaLojaIFood.Offline, StatusDaLojaIFood.Disponibilidade(json));
    }

    // Loja aberta no salão mas fechada para delivery não pode aparecer online:
    // o painel existe para despachar entrega.
    [Fact]
    public void Disponibilidade_deve_ignorar_operacoes_que_nao_sao_entrega()
    {
        var json = """
        [
          { "operation": "TAKEOUT",    "salesChannel": "IFOOD", "available": true },
          { "operation": "DINE_IN",    "salesChannel": "IFOOD", "available": true },
          { "operation": "DELIVERY",   "salesChannel": "IFOOD", "available": false }
        ]
        """;

        Assert.Equal(StatusDaLojaIFood.Offline, StatusDaLojaIFood.Disponibilidade(json));
    }

    // Loja com um canal só não precisa declarar qual é.
    [Fact]
    public void Disponibilidade_deve_usar_a_unica_entrega_quando_o_canal_nao_bate()
    {
        var json = """
        [{ "operation": "DELIVERY", "salesChannel": "OUTRO_PARCEIRO", "available": true }]
        """;

        Assert.Equal(StatusDaLojaIFood.Online, StatusDaLojaIFood.Disponibilidade(json));
    }

    // Com mais de uma entrega e nenhuma no canal do iFood, escolher qualquer
    // uma seria chutar: pode ser o status de um marketplace terceiro.
    [Fact]
    public void Disponibilidade_deve_ser_indisponivel_com_varias_entregas_e_nenhuma_do_ifood()
    {
        var json = """
        [
          { "operation": "DELIVERY", "salesChannel": "PARCEIRO_A", "available": true },
          { "operation": "DELIVERY", "salesChannel": "PARCEIRO_B", "available": false }
        ]
        """;

        Assert.Equal(StatusDaLojaIFood.Indisponivel, StatusDaLojaIFood.Disponibilidade(json));
    }

    [Fact]
    public void Disponibilidade_deve_aceitar_operacao_e_canal_em_qualquer_caixa()
    {
        var json = """
        [{ "operation": "delivery", "salesChannel": "iFood", "available": true }]
        """;

        Assert.Equal(StatusDaLojaIFood.Online, StatusDaLojaIFood.Disponibilidade(json));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("""{ "operation": "DELIVERY", "available": true }""")]
    [InlineData("null")]
    [InlineData("""[{ "operation": "DELIVERY", "salesChannel": "IFOOD" }]""")]
    [InlineData("""[{ "operation": "DELIVERY", "salesChannel": "IFOOD", "available": "sim" }]""")]
    [InlineData("""["texto solto"]""")]
    public void Disponibilidade_deve_ser_indisponivel_para_payload_inesperado(string json)
    {
        Assert.Equal(StatusDaLojaIFood.Indisponivel, StatusDaLojaIFood.Disponibilidade(json));
    }

    [Fact]
    public void Nome_deve_extrair_o_nome_da_loja()
    {
        var json = """{ "name": "Teste - 60.830.795 JONATHAN", "id": "abc" }""";

        Assert.Equal("Teste - 60.830.795 JONATHAN", StatusDaLojaIFood.Nome(json));
    }

    [Theory]
    [InlineData("""{ "id": "abc" }""")]
    [InlineData("""{ "name": 42 }""")]
    [InlineData("""{ "name": null }""")]
    [InlineData("[]")]
    [InlineData("null")]
    public void Nome_deve_ser_nulo_quando_o_payload_nao_traz_nome_textual(string json)
    {
        Assert.Null(StatusDaLojaIFood.Nome(json));
    }
}
