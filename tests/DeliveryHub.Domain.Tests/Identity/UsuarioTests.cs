using DeliveryHub.Domain.Identity;

namespace DeliveryHub.Domain.Tests.Identity;

public sealed class UsuarioTests
{
    private static Usuario Novo() => Usuario.Criar(
        "Dono@Loja.com  ",
        "hash-irrelevante",
        "Dono da Loja",
        PapelUsuario.DonoRestaurante,
        DateTimeOffset.UtcNow);

    // 1x1 PNG transparente — o menor arquivo de imagem válido que existe.
    private const string PngMinimo =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    [Fact]
    public void Usuario_nasce_sem_foto()
    {
        Assert.Null(Novo().FotoBase64);
    }

    [Fact]
    public void Define_a_foto_quando_o_formato_e_aceito()
    {
        var usuario = Novo();

        var resultado = usuario.DefinirFoto(PngMinimo);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(PngMinimo, usuario.FotoBase64);
    }

    [Theory]
    [InlineData("data:image/jpeg;base64,")]
    [InlineData("data:image/webp;base64,")]
    public void Aceita_os_outros_formatos_que_o_navegador_gera(string prefixo)
    {
        var usuario = Novo();
        var conteudo = PngMinimo[(PngMinimo.IndexOf(',') + 1)..];

        Assert.True(usuario.DefinirFoto(prefixo + conteudo).IsSuccess);
    }

    [Fact]
    public void Recusa_svg_mesmo_sendo_imagem()
    {
        // SVG é documento com script: renderizado em <img> no painel de quem
        // abre o perfil, viraria execução de código de terceiro.
        var usuario = Novo();
        var svg = "data:image/svg+xml;base64,PHN2Zz48L3N2Zz4=";

        var resultado = usuario.DefinirFoto(svg);

        Assert.True(resultado.IsFailure);
        Assert.Equal(UsuarioErrors.FotoInvalida, resultado.Error);
        Assert.Null(usuario.FotoBase64);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://exemplo.com/foto.png")]
    [InlineData("data:text/html;base64,PGgxPm9pPC9oMT4=")]
    [InlineData("data:image/png;base64,")]
    [InlineData("data:image/png;base64,isto-nao-e-base64!!")]
    public void Recusa_conteudo_que_nao_e_imagem_valida(string entrada)
    {
        var usuario = Novo();

        Assert.True(usuario.DefinirFoto(entrada).IsFailure);
        Assert.Null(usuario.FotoBase64);
    }

    [Fact]
    public void Recusa_imagem_acima_do_teto()
    {
        // O navegador reduz antes de enviar; este caso é quem chama a API
        // direto e tentaria usar a coluna como depósito de arquivo.
        var usuario = Novo();
        var gigante = "data:image/png;base64," + new string('A', 700_001);

        var resultado = usuario.DefinirFoto(gigante);

        Assert.True(resultado.IsFailure);
        Assert.Equal(UsuarioErrors.FotoGrande, resultado.Error);
    }

    [Fact]
    public void Trocar_a_foto_substitui_a_anterior()
    {
        var usuario = Novo();
        usuario.DefinirFoto(PngMinimo);
        var conteudo = PngMinimo[(PngMinimo.IndexOf(',') + 1)..];
        var nova = "data:image/webp;base64," + conteudo;

        usuario.DefinirFoto(nova);

        Assert.Equal(nova, usuario.FotoBase64);
    }

    [Fact]
    public void Foto_recusada_nao_apaga_a_que_ja_existia()
    {
        var usuario = Novo();
        usuario.DefinirFoto(PngMinimo);

        usuario.DefinirFoto("data:image/png;base64,quebrado!!");

        Assert.Equal(PngMinimo, usuario.FotoBase64);
    }
}
