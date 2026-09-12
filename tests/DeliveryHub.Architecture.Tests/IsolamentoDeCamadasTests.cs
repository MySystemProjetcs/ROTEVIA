using System.Reflection;
using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.SharedKernel;
using NetArchTest.Rules;

namespace DeliveryHub.Architecture.Tests;

// Estes testes quebram o build de propósito. O isolamento entre camadas é a
// única coisa que impede o formato do iFood de vazar para dentro do domínio, e
// disciplina não sobrevive a prazo apertado — regra automatizada sobrevive.
public sealed class IsolamentoDeCamadasTests
{
    private static readonly Assembly Domain = typeof(ITenantOwned).Assembly;
    private static readonly Assembly Application = typeof(IOrderSource).Assembly;
    private static readonly Assembly Infrastructure =
        typeof(Infrastructure.Persistence.AppDbContext).Assembly;

    private const string NamespaceInfrastructure = "DeliveryHub.Infrastructure";
    private const string NamespaceContratosIFood = "DeliveryHub.Infrastructure.Integrations.IFood.Contracts";

    [Fact]
    public void Domain_nao_depende_de_nenhuma_outra_camada()
    {
        var resultado = Types.InAssembly(Domain)
            .Should()
            .NotHaveDependencyOnAny(NamespaceInfrastructure, "DeliveryHub.Application")
            .GetResult();

        Assert.True(resultado.IsSuccessful, Descrever(resultado));
    }

    [Fact]
    public void Application_nao_depende_de_Infrastructure()
    {
        // A Application declara portas; quem as implementa é a Infrastructure.
        // Se a seta inverter, o hexágono deixa de existir.
        var resultado = Types.InAssembly(Application)
            .Should()
            .NotHaveDependencyOn(NamespaceInfrastructure)
            .GetResult();

        Assert.True(resultado.IsSuccessful, Descrever(resultado));
    }

    [Fact]
    public void Contratos_do_iFood_nao_vazam_para_Domain_nem_Application()
    {
        // O domínio conhece Pedido e Entrega — nunca IFoodOrderDetails
        // (CLAUDE.md §4). O mapper é a única fronteira de tradução.
        foreach (var assembly in new[] { Domain, Application })
        {
            var resultado = Types.InAssembly(assembly)
                .Should()
                .NotHaveDependencyOn(NamespaceContratosIFood)
                .GetResult();

            Assert.True(resultado.IsSuccessful, Descrever(resultado));
        }
    }

    [Fact]
    public void Contratos_do_iFood_sao_internos_ao_assembly_de_Infrastructure()
    {
        // Ser internal é o que torna o vazamento impossível por construção, e
        // não apenas proibido por convenção.
        var publicos = Types.InAssembly(Infrastructure)
            .That()
            .ResideInNamespace(NamespaceContratosIFood)
            .And()
            .ArePublic()
            .GetTypes();

        Assert.Empty(publicos);
    }

    [Fact]
    public void Domain_nao_conhece_EF_Core_nem_HttpClient()
    {
        // Domínio com dependência de persistência ou de rede deixa de ser
        // testável sem infraestrutura.
        var resultado = Types.InAssembly(Domain)
            .Should()
            .NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "System.Net.Http")
            .GetResult();

        Assert.True(resultado.IsSuccessful, Descrever(resultado));
    }

    private static string Descrever(TestResult resultado) =>
        resultado.IsSuccessful
            ? string.Empty
            : "Tipos violando a regra: " + string.Join(", ", resultado.FailingTypeNames ?? []);
}
