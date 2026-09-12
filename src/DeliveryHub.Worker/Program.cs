using System.Text.Json;
using DeliveryHub.Application.Abstractions;
using DeliveryHub.Infrastructure.Integrations.IFood;
using DeliveryHub.Infrastructure.Persistence;
using DeliveryHub.Worker;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// A ingestão opera entre tenants: o iFood entrega os eventos de todas as lojas
// num fluxo só e é aqui que o merchant é resolvido, evento a evento. Este é o
// único lugar do sistema onde o filtro por tenant fica aberto.
builder.Services.AddSingleton<ITenantContext, TenantContextSistema>();

builder.Services.AddIFoodIntegration(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddHostedService<IFoodPollingWorker>();

builder.Services.AddHealthChecks()
    .AddCheck<PollingHealthCheck>("polling-ifood", tags: ["ingestao"]);

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

app.Run();
