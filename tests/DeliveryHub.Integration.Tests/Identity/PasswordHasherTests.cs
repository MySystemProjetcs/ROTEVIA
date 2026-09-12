using DeliveryHub.Infrastructure.Identity;

namespace DeliveryHub.Integration.Tests.Identity;

public sealed class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Senha_correta_verifica()
    {
        var hash = _hasher.Hash("SenhaDoLojista#1");

        Assert.True(_hasher.Verificar("SenhaDoLojista#1", hash));
    }

    [Fact]
    public void Senha_errada_nao_verifica()
    {
        var hash = _hasher.Hash("SenhaDoLojista#1");

        Assert.False(_hasher.Verificar("SenhaDoLojista#2", hash));
    }

    [Fact]
    public void Mesma_senha_gera_hashes_diferentes()
    {
        // Salt por senha: sem isso, duas contas com a mesma senha teriam o
        // mesmo hash e um vazamento do banco revelaria isso de imediato.
        Assert.NotEqual(_hasher.Hash("mesma"), _hasher.Hash("mesma"));
    }

    [Fact]
    public void Hash_guarda_o_custo_para_permitir_aumento_futuro()
    {
        // Formato "iteracoes.salt.hash": subir o custo depois não invalida as
        // senhas já cadastradas.
        var partes = _hasher.Hash("qualquer").Split('.');

        Assert.Equal(3, partes.Length);
        Assert.True(int.Parse(partes[0]) >= 100_000);
    }

    [Fact]
    public void Hash_corrompido_nao_derruba_o_login()
    {
        Assert.False(_hasher.Verificar("qualquer", "lixo"));
    }

    [Fact]
    public void Senha_gerada_tem_tamanho_util_e_nao_repete()
    {
        var gerador = new GeradorDeSenha();
        var senhas = Enumerable.Range(0, 50).Select(_ => gerador.Gerar()).ToList();

        Assert.All(senhas, s => Assert.Equal(12, s.Length));
        Assert.Equal(senhas.Count, senhas.Distinct().Count());

        // Sem caracteres ambíguos: a senha é lida e digitada por uma pessoa.
        Assert.All(senhas, s => Assert.DoesNotContain(s, c => c is 'O' or '0' or 'l' or 'I' or '1'));
    }
}
