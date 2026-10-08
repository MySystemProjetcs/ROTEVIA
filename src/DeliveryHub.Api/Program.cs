using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using DeliveryHub.Api.Diagnostics;
using DeliveryHub.Api.Identity;
using DeliveryHub.Api.Integracoes;
using DeliveryHub.Api.Orders;
using DeliveryHub.Infrastructure.RealTime;
using DeliveryHub.Application.Abstractions;
using DeliveryHub.Api.Couriers;
using DeliveryHub.Api.Dashboard;
using DeliveryHub.Api.Merchants;
using DeliveryHub.Api.WhatsApp;
using DeliveryHub.Application.Couriers;
using DeliveryHub.Application.Identity;
using DeliveryHub.Application.Merchants;
using DeliveryHub.Application.Orders;
using DeliveryHub.Infrastructure.Identity;
using DeliveryHub.Infrastructure.Integrations.Enderecos;
using DeliveryHub.Infrastructure.Integrations.DiDiFood;
using DeliveryHub.Infrastructure.Integrations.IFood;
using DeliveryHub.Infrastructure.Integrations.WhatsApp;
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

        // SignalR via WebSocket não suporta header Authorization padrão:
        // o navegador não permite headers customizados em conexões WS.
        // A convenção do SignalR JS client é enviar o token como query string
        // "access_token" — este handler captura e repassa como Bearer.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddPoliticasDeAutorizacao();
builder.Services.AddHttpContextAccessor();

// ProblemDetails + ExceptionHandler globais: sem isso, uma exceção não tratada
// vaza mensagem interna (ex.: "Jwt:ChaveAssinatura não configurada") no body
// quando a página de desenvolvedor não está ativa.
builder.Services.AddProblemDetails();

// Rate limit: janela fixa de 10 tentativas por minuto por IP no fluxo de login
// e cadastro. É proteção de infraestrutura contra brute-force — o PBKDF2 é
// lento, mas sem throttling o atacante paga o custo do lado dele.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// O tenant sai do token. Este registro é o que separa a API do worker: aqui o
// isolamento vale, lá o processo opera entre tenants por natureza.
builder.Services.AddScoped<ITenantContext, TenantContextHttp>();

builder.Services.AddIFoodIntegration(builder.Configuration);
builder.Services.AddDiDiFoodIntegration(builder.Configuration);
builder.Services.AddWhatsAppIntegration(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddBuscaDeEndereco(builder.Configuration);
builder.Services.AddIdentityInfrastructure(builder.Configuration);

builder.Services.AddScoped<IAvancarPedido, AvancarPedido>();
builder.Services.AddScoped<ICancelarPedido, CancelarPedido>();
builder.Services.AddScoped<IDespacharEmLote, DespacharEmLote>();
builder.Services.AddScoped<ILancarPedidoInterno, LancarPedidoInterno>();
builder.Services.AddScoped<IAlocarEntregador, AlocarEntregador>();
builder.Services.AddScoped<IAvancarEntrega, AvancarEntrega>();
builder.Services.AddScoped<IConfirmarEntregaComCodigo, ConfirmarEntregaComCodigo>();
builder.Services.AddScoped<IConfirmarColetaComCodigo, ConfirmarColetaComCodigo>();
builder.Services.AddScoped<IAutenticar, Autenticar>();
builder.Services.AddScoped<ICadastrarRestaurante, CadastrarRestaurante>();
builder.Services.AddScoped<ICriarDonoParaRestaurante, CriarDonoParaRestaurante>();
builder.Services.AddScoped<IIniciarConexaoIFood, IniciarConexaoIFood>();
builder.Services.AddScoped<IConfirmarConexaoIFood, ConfirmarConexaoIFood>();
builder.Services.AddScoped<IConvidarEntregador, ConvidarEntregador>();
builder.Services.AddScoped<IConfirmarConviteEntregador, ConfirmarConviteEntregador>();
builder.Services.AddScoped<IListarEntregadores, ListarEntregadores>();
builder.Services.AddScoped<IObterPreviaDoConvite, ObterPreviaDoConvite>();
builder.Services.AddScoped<IDisponibilidadeEntrega, DisponibilidadeEntrega>();
builder.Services.AddScoped<IDefinirTaxaPorEntrega, DefinirTaxaPorEntrega>();
builder.Services.AddScoped<IDefinirEnderecoDaLoja, DefinirEnderecoDaLoja>();
builder.Services.AddScoped<IDefinirNomeDaLoja, DefinirNomeDaLoja>();
builder.Services.AddScoped<IAtualizarCadastroDoEntregador, AtualizarCadastroDoEntregador>();
builder.Services.AddScoped<IRemoverVinculoDoEntregador, RemoverVinculoDoEntregador>();
builder.Services.AddScoped<IAlterarEmailDoUsuario, AlterarEmailDoUsuario>();
builder.Services.AddScoped<IDefinirFotoDePerfil, DefinirFotoDePerfil>();

// SignalR com backplane no Redis: é o que permite o worker de polling, que
// vive em outro container de DI, alcançar as conexões abertas aqui.
builder.Services.AddTempoReal(builder.Configuration);

var app = builder.Build();

await app.SemearAdministradorAsync();

// Handler global de exceção: converte falha não tratada em ProblemDetails,
// sem revelar stack trace nem mensagem interna. Em Development o ASP.NET
// mantém a página de diagnóstico por padrão.
if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapPedidoEndpoints();
app.MapEntregaEndpoints();
app.MapRastreioEndpoints();
app.MapPedidoInternoEndpoints();
app.MapMerchantEndpoints();
app.MapCourierEndpoints();
app.MapDashboardEndpoints();
app.MapPerfilEndpoints();
app.MapWhatsAppEndpoints();
app.MapNoventaENoveWebhookEndpoints();
app.MapIFoodDiagnosticsEndpoints();
app.MapHub<RastreioHub>("/hubs/rastreio");

// Gerador de pedido com endereço real: só existe fora de produção.
if (app.Environment.IsDevelopment())
    app.MapPedidoLocalEndpoints();

// Worker embutido: container de DI próprio (TenantContextSistema não pode
// coexistir com o TenantContextHttp desta API), só compartilhando o processo
// e uma porta própria pro /health do polling ficar visível separado do da API.
// Configurável porque a 5000 é disputada: no macOS o Receptor AirPlay ocupa
// ela sozinho, e aí a API inteira deixa de subir por causa do worker.
var workerHost = DeliveryHub.Worker.WorkerHostFactory.Build(
    urls: builder.Configuration["Worker:HealthUrl"] ?? "http://localhost:5000");
await workerHost.StartAsync();

try
{
    await app.RunAsync();
}
finally
{
    await workerHost.StopAsync();
    await workerHost.DisposeAsync();
}
