using DeliveryHub.Infrastructure.Integrations.IFood;
using DeliveryHub.Infrastructure.Integrations.IFood.Auth;
using DeliveryHub.Infrastructure.Integrations.IFood.Orders;
using DeliveryHub.Infrastructure.Integrations.IFood.Polling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeliveryHub.Integration.Tests.IFood;

public sealed class IFoodServiceRegistrationTests
{
    private static ServiceProvider Build()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IFood:ClientId"] = "client-id-de-teste",
                ["IFood:ClientSecret"] = "client-secret-de-teste"
            })
            .Build();

        return new ServiceCollection()
            .AddIFoodIntegration(configuration)
            .BuildServiceProvider();
    }

    [Fact]
    public void Autenticador_e_singleton_para_o_cache_do_token_sobreviver()
    {
        // Como transient, cada injeção começaria sem token em cache e
        // reautenticaria — excesso de requisição bloqueia o app no iFood.
        using var provider = Build();

        var primeiro = provider.GetRequiredService<IIFoodAuthenticator>();
        var segundo = provider.GetRequiredService<IIFoodAuthenticator>();

        Assert.Same(primeiro, segundo);
    }

    [Fact]
    public void Clients_de_recurso_resolvem_com_a_base_address_do_modulo()
    {
        using var provider = Build();

        Assert.NotNull(provider.GetRequiredService<IIFoodEventsClient>());
        Assert.NotNull(provider.GetRequiredService<IIFoodOrderClient>());
    }
}
