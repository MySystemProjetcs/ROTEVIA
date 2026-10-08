using System.Text.Json;
using DeliveryHub.Application.Abstractions;
using DeliveryHub.Infrastructure.Integrations.IFood;
using DeliveryHub.Infrastructure.Persistence;
using DeliveryHub.Infrastructure.RealTime;
using DeliveryHub.Worker.Health;
using DeliveryHub.Worker.Resilience;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace DeliveryHub.Worker;

// Composição do processo de ingestão, extraída do Program.cs pra ser
// reaproveitada tanto rodando standalone (dotnet run do Worker) quanto
// embutida dentro do processo da API — por pedido explícito, divergindo do
// desenho original do CLAUDE.md de processos sempre separados.
//
// Sem args do chamador de propósito: se o processo hospedeiro tiver sido
// iniciado com --urls apontando pra própria porta dele (ex.: a API com
// --urls http://localhost:5300), repassar esses args faria este builder
// tentar herdar o mesmo endereço e colidir na porta.
public static class WorkerHostFactory
{
    public static WebApplication Build(string urls)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(urls);

        // A ingestão opera entre tenants: o iFood entrega os eventos de todas
        // as lojas num fluxo só e é aqui que o merchant é resolvido, evento a
        // evento. Este é o único lugar do sistema onde o filtro por tenant
        // fica aberto — nunca registrar TenantContextSistema num host que
        // também atenda requisição HTTP autenticada (por isso este host tem
        // seu próprio container de DI, isolado do host que o embute).
        builder.Services.AddSingleton<ITenantContext, TenantContextSistema>();

        builder.Services.AddIFoodIntegration(builder.Configuration);
        builder.Services.AddPersistence(builder.Configuration);

        // Resiliência por loja (timeout + circuit breaker) e estado do lag do
        // Inbox: singletons porque o estado sobrevive entre ciclos e é lido
        // pelo health check de outro container de DI.
        builder.Services.AddSingleton(new MerchantResilienceOptions());
        builder.Services.AddSingleton<MerchantResilienceProvider>();
        builder.Services.AddSingleton<InboxLagState>();

        builder.Services.AddHostedService<IFoodPollingWorker>();
        builder.Services.AddHostedService<IFoodInboxProcessorWorker>();

        // Gauges do Inbox registrados no boot (antes do primeiro scrape).
        MetricasPolling.Inicializar();

        // O mesmo backplane da API. Este host não atende nenhuma conexão de
        // SignalR — ele só publica, e o Redis entrega a quem está conectado
        // lá. Sem isso, pedido ingerido aqui só apareceria no próximo poll.
        builder.Services.AddTempoReal(builder.Configuration);

        builder.Services.AddHealthChecks()
            .AddCheck<PollingHealthCheck>("polling-ifood", tags: ["ingestao"])
            .AddCheck<InboxLagHealthCheck>("inbox-lag", tags: ["ingestao"]);

        var app = builder.Build();

        app.MapHealthChecks("/health", new()
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";

                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.ToDictionary(
                        e => e.Key,
                        e => new { status = e.Value.Status.ToString(), descricao = e.Value.Description, dados = e.Value.Data })
                }));
            }
        });

        return app;
    }
}
