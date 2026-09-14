using DeliveryHub.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace DeliveryHub.Infrastructure.RealTime;

public static class RealTimeServiceCollectionExtensions
{
    // Backplane no Redis porque quem ingere o pedido e quem mantém as conexões
    // são containers de DI diferentes: o worker de polling não alcança o
    // HubLifetimeManager da API. Sem isso, pedido vindo do iFood só aparece no
    // próximo poll da tela.
    //
    // Vale também quando a API rodar em mais de uma instância — cada uma só
    // conhece as próprias conexões, e o backplane é o que costura as duas.
    //
    // Sem Redis configurado o SignalR continua funcionando em memória: o push
    // da própria API seque valendo, só a ponte entre processos deixa de existir.
    public static IServiceCollection AddTempoReal(this IServiceCollection services, IConfiguration configuration)
    {
        var redis = configuration.GetConnectionString("Redis");

        var signalR = services.AddSignalR();

        if (string.IsNullOrWhiteSpace(redis))
        {
            // Sem Redis o cache vale só dentro deste processo — que é
            // justamente o cenário de quem roda sem Redis.
            services.AddSingleton<ICachePosicoes, CachePosicoesMemoria>();
        }
        else
        {
            signalR.AddStackExchangeRedis(redis);
            services.AddSingleton<IConnectionMultiplexer>(
                _ => ConnectionMultiplexer.Connect(redis));
            services.AddSingleton<ICachePosicoes, CachePosicoesRedis>();
        }

        services.AddScoped<INotificadorPainel, NotificadorPainel>();

        return services;
    }
}
