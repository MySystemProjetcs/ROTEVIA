using DeliveryHub.Application.Orders;

namespace DeliveryHub.Integration.Tests.Orders;

public sealed class SequenciadorDeRotaTests
{
    [Fact]
    public void Ordena_por_vizinho_mais_proximo_a_partir_da_loja()
    {
        var longe = Guid.NewGuid();
        var perto = Guid.NewGuid();
        var meio = Guid.NewGuid();

        // Loja em (0,0). perto=1, meio=4, longe=10 de distância no eixo.
        var ordem = SequenciadorDeRota.Ordenar(0, 0, new[]
        {
            new ParadaDaRota(longe, 0, 10),
            new ParadaDaRota(perto, 0, 1),
            new ParadaDaRota(meio, 0, 4),
        });

        Assert.Equal(new[] { perto, meio, longe }, ordem);
    }

    [Fact]
    public void Parada_sem_coordenada_vai_para_o_fim()
    {
        var comCoord = Guid.NewGuid();
        var semCoord = Guid.NewGuid();

        var ordem = SequenciadorDeRota.Ordenar(0, 0, new[]
        {
            new ParadaDaRota(semCoord, null, null),
            new ParadaDaRota(comCoord, 0, 2),
        });

        Assert.Equal(new[] { comCoord, semCoord }, ordem);
    }

    [Fact]
    public void Coordenada_zero_zero_conta_como_ponto_valido_apenas_se_passada()
    {
        // O sequenciador não julga 0,0 — quem descarta é o caso de uso. Aqui,
        // passar (0,0) explícito significa "está na loja": distância zero,
        // então vem primeiro.
        var naLoja = Guid.NewGuid();
        var distante = Guid.NewGuid();

        var ordem = SequenciadorDeRota.Ordenar(0, 0, new[]
        {
            new ParadaDaRota(distante, 0, 5),
            new ParadaDaRota(naLoja, 0, 0),
        });

        Assert.Equal(new[] { naLoja, distante }, ordem);
    }
}
