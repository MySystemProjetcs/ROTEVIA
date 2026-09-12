using System.Text;
using System.Text.Json.Serialization;
using DeliveryHub.Api.Diagnostics;
using DeliveryHub.Api.Identity;
using DeliveryHub.Api.Orders;
using DeliveryHub.Application.Abstractions;
using DeliveryHub.Application.Identity;
using DeliveryHub.Application.Orders;
using DeliveryHub.Infrastructure.Identity;
using DeliveryHub.Infrastructure.Integrations.IFood;
using DeliveryHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Enum como nome, não como número: "Confirmado" sobrevive a reordenação do
// enum, o índice 1 não. O contrato com o cliente fica estável.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Emissor"],
            ValidAudience = jwt["Audiencia"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwt["ChaveAssinatura"]
                    ?? throw new InvalidOperationException("Jwt:ChaveAssinatura não configurada."))),
            // Sem tolerância de relógio: token expirado é token expirado.
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddPoliticasDeAutorizacao();
builder.Services.AddHttpContextAccessor();

// O tenant sai do token. Este registro é o que separa a API do worker: aqui o
// isolamento vale, lá o processo opera entre tenants por natureza.
builder.Services.AddScoped<ITenantContext, TenantContextHttp>();

builder.Services.AddIFoodIntegration(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddIdentityInfrastructure(builder.Configuration);

builder.Services.AddScoped<IAvancarPedido, AvancarPedido>();
builder.Services.AddScoped<IAutenticar, Autenticar>();
builder.Services.AddScoped<ICadastrarRestaurante, CadastrarRestaurante>();
builder.Services.AddScoped<ICriarDonoParaRestaurante, CriarDonoParaRestaurante>();

var app = builder.Build();

await app.SemearAdministradorAsync();

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapPedidoEndpoints();
app.MapIFoodDiagnosticsEndpoints();

app.Run();
