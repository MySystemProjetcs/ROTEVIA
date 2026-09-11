using DeliveryHub.Api.Diagnostics;
using DeliveryHub.Infrastructure.Integrations.IFood;
using DeliveryHub.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIFoodIntegration(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration);

var app = builder.Build();

app.MapGet("/", () => "Hello World!");
app.MapIFoodDiagnosticsEndpoints();

app.Run();
