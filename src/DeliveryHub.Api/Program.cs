using DeliveryHub.Api.Diagnostics;
using DeliveryHub.Infrastructure.Integrations.IFood;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIFoodIntegration(builder.Configuration);

var app = builder.Build();

app.MapGet("/", () => "Hello World!");
app.MapIFoodDiagnosticsEndpoints();

app.Run();
