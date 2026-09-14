using DeliveryHub.Domain.SharedKernel;
using DeliveryHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DeliveryHub.Integration.Tests.Persistence;

// Camada 4 do isolamento (CLAUDE.md §6). Foi exatamente esta regra que faltava
// quando pedido_itens nasceu sem tenant: a entidade era alcançável só pelo
// Pedido, então o furo passou despercebido até alguém olhar. Agora não passa.
public sealed class QueryFilterObrigatorioTests
{
    private static AppDbContext Contexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=deliveryhub;Username=deliveryhub;Password=deliveryhub_dev")
            .Options,
            new TenantContextSistema(),
            new CifradorDeTeste());

    [Fact]
    public void Toda_entidade_ITenantOwned_tem_query_filter_registrado()
    {
        using var db = Contexto();

        var semFiltro = db.Model.GetEntityTypes()
            .Where(e => typeof(ITenantOwned).IsAssignableFrom(e.ClrType))
            .Where(e => e.GetQueryFilter() is null)
            .Select(e => e.ClrType.Name)
            .ToList();

        Assert.True(semFiltro.Count == 0,
            "Entidades ITenantOwned sem filtro por tenant: " + string.Join(", ", semFiltro));
    }

    [Fact]
    public void Existe_ao_menos_uma_entidade_ITenantOwned_mapeada()
    {
        // Sem esta guarda, o teste acima passaria trivialmente se ninguém mais
        // implementasse ITenantOwned — e daria a impressão de que está tudo
        // protegido quando na verdade nada está sendo verificado.
        using var db = Contexto();

        var tenantOwned = db.Model.GetEntityTypes()
            .Count(e => typeof(ITenantOwned).IsAssignableFrom(e.ClrType));

        Assert.True(tenantOwned >= 2, $"Esperava entidades ITenantOwned mapeadas, encontrei {tenantOwned}.");
    }
}
