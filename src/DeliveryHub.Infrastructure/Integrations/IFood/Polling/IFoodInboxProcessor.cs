using System.Text.Json;
using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Orders;
using DeliveryHub.Domain.SharedKernel;
using DeliveryHub.Infrastructure.Integrations.IFood.Orders;
using DeliveryHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Polling;

public interface IIFoodInboxProcessor
{
    Task<int> ProcessarPendentesAsync(int limite, CancellationToken ct);
}

// Roda separado do polling (CLAUDE.md §7): a ingestão grava e reconhece rápido,
// o trabalho pesado — buscar detalhe do pedido — acontece aqui, fora da janela
// de 30s do heartbeat.
//
// A fila do inbox mistura evento de loja do Centralizado com evento de loja do
// Distribuído — quem trata isso é este processador, buscando o merchant dono
// da entrada e usando o token certo para pedir o detalhe do pedido.
internal sealed class IFoodInboxProcessor : IIFoodInboxProcessor
{
    private readonly AppDbContext _db;
    private readonly IIFoodOrderClient _orders;
    private readonly IIFoodMerchantTokenProvider _tokenProvider;
    private readonly TimeProvider _timeProvider;
    private readonly INotificadorPainel _notificador;
    private readonly ILogger<IFoodInboxProcessor> _logger;

    public IFoodInboxProcessor(
        AppDbContext db,
        IIFoodOrderClient orders,
        IIFoodMerchantTokenProvider tokenProvider,
        TimeProvider timeProvider,
        INotificadorPainel notificador,
        ILogger<IFoodInboxProcessor> logger)
    {
        _db = db;
        _orders = orders;
        _tokenProvider = tokenProvider;
        _timeProvider = timeProvider;
        _notificador = notificador;
        _logger = logger;
    }

    public async Task<int> ProcessarPendentesAsync(int limite, CancellationToken ct)
    {
        // Eventos em quarentena (merchant_id nulo) ficam de fora: são
        // reprocessáveis depois que a loja for cadastrada.
        var pendentes = await _db.IntegrationInbox
            .Where(x => x.ProcessedAt == null && x.MerchantId != null && x.Source == "ifood")
            .OrderBy(x => x.ReceivedAt)
            .Take(limite)
            .ToListAsync(ct);

        var processados = 0;

        foreach (var entrada in pendentes)
        {
            try
            {
                await ProcessarAsync(entrada, ct);
                entrada.MarcarProcessado(_timeProvider.GetUtcNow());
                await _db.SaveChangesAsync(ct);
                processados++;

                // Só depois de persistido: avisar antes deixaria a tela buscar
                // um estado que ainda não existe no banco. Vai pelo backplane
                // do Redis, porque quem está conectado é a API, não este host.
                await _notificador.ResumoAtualizadoAsync(entrada.MerchantId!.Value, ct);
            }
            catch (Exception ex)
            {
                // Uma entrada ruim não pode travar a fila inteira: ela fica
                // pendente e é retentada no próximo ciclo.
                _db.ChangeTracker.Clear();
                _logger.LogError(ex, "Falha ao processar evento {EventoId} do inbox.", entrada.ExternalEventId);
            }
        }

        return processados;
    }

    private async Task ProcessarAsync(IntegrationInboxEvent entrada, CancellationToken ct)
    {
        var evento = JsonSerializer.Deserialize<Contracts.IFoodEvent>(entrada.Payload)
            ?? throw new InvalidOperationException("Payload do inbox ilegível.");

        var idExterno = evento.OrderId.ToString();

        var pedido = await _db.Pedidos
            .FirstOrDefaultAsync(x => x.IdExterno == idExterno, ct);

        if (pedido is null)
        {
            // Só o PLACED cria pedido. Qualquer outro evento sem pedido
            // correspondente chegou fora de ordem — o PLACED ainda está na fila.
            if (evento.FullCode != "PLACED")
            {
                _logger.LogWarning(
                    "Evento {FullCode} do pedido {PedidoId} chegou antes do PLACED; será retentado.",
                    evento.FullCode, idExterno);
                throw new InvalidOperationException("Pedido ainda não existe.");
            }

            var autorizacao = await ResolverAutorizacaoAsync(entrada.MerchantId!.Value, ct);
            var detalhe = await _orders.GetDetailsAsync(evento.OrderId, ct, autorizacao);
            pedido = IFoodOrderMapper.ParaPedido(detalhe, entrada.MerchantId!.Value, entrada.ReceivedAt);
            _db.Pedidos.Add(pedido);
            await ConfirmarAutomaticamenteAsync(pedido, evento.OrderId, ct);
            return;
        }

        AplicarTransicao(pedido, evento.FullCode);

        // Segundo caminho do fato gerador (o primeiro é o Finalizar do
        // motoboy): iFood marcando CONCLUDED também encerra a entrega aqui.
        // Mesma regra do AvancarEntrega — concluído com entregador gera
        // repasse, com a taxa vigente (snapshot, nunca recalculado), inclusive
        // para pedido de teste.
        if (evento.FullCode == "CONCLUDED"
            && pedido.Status == StatusPedido.Concluido
            && pedido.EntregadorId is not null
            && !pedido.ValorPagoAoEntregador.HasValue)
        {
            var taxa = await _db.Merchants
                .Where(x => x.Id == pedido.MerchantId)
                .Select(x => (decimal?)x.TaxaPadraoPorEntrega)
                .FirstOrDefaultAsync(ct);

            if (taxa.HasValue)
                pedido.RegistrarRepasseAoEntregador(taxa.Value);
        }
    }

    // Confirmação automática: o pedido já entra confirmado, sem depender de o
    // lojista clicar dentro da janela do iFood. Origem primeiro, domínio depois
    // — mesma ordem do AvancarPedido, para não mostrar um estado que não existe
    // lá fora.
    //
    // Melhor esforço de propósito: se o iFood recusar ou a rede cair, o pedido
    // segue em "Recebido" e o botão Confirmar do painel continua valendo como
    // saída manual. Deixar a exceção subir faria o evento ser retentado e o
    // pedido nem chegaria ao quadro.
    private async Task ConfirmarAutomaticamenteAsync(Pedido pedido, Guid orderId, CancellationToken ct)
    {
        try
        {
            await _orders.ConfirmAsync(orderId, ct);
        }
        catch (Exception ex) when (ex is IFoodApiException or HttpRequestException)
        {
            _logger.LogWarning(
                ex,
                "Confirmação automática do pedido {PedidoId} falhou; segue em Recebido para confirmação manual.",
                pedido.IdExterno);
            return;
        }

        var resultado = pedido.Confirmar();

        if (resultado.IsFailure)
        {
            _logger.LogWarning(
                "Confirmação automática recusada pelo domínio no pedido {PedidoId}: {Erro}",
                pedido.IdExterno, resultado.Error.Code);
        }
    }

    // Nulo pede o token Centralizado padrão (handler injeta sozinho). Loja
    // conectada pelo Distribuído tem seu próprio token — usar o Centralizado
    // nela devolveria 401, o token dele não enxerga essa loja.
    private async Task<System.Net.Http.Headers.AuthenticationHeaderValue?> ResolverAutorizacaoAsync(
        Guid merchantId, CancellationToken ct)
    {
        var merchant = await _db.Merchants.FirstOrDefaultAsync(x => x.Id == merchantId, ct);

        return merchant?.ConexaoIFood?.Conectado == true
            ? await _tokenProvider.ObterAsync(merchant, ct)
            : null;
    }

    private void AplicarTransicao(Pedido pedido, string fullCode)
    {
        var resultado = fullCode switch
        {
            "PLACED" => Result.Success(),
            "CONFIRMED" => pedido.Confirmar(),
            "PREPARATION_STARTED" => pedido.IniciarPreparo(),
            "READY_TO_PICKUP" => pedido.MarcarPronto(),
            "DISPATCHED" => pedido.Despachar(),
            "CONCLUDED" => pedido.Concluir(),
            "CANCELLED" => pedido.Cancelar("Cancelado pela origem (iFood)."),
            // Eventos que não mudam status (logística, handshake, patch) são
            // reconhecidos e marcados como processados sem efeito no pedido.
            _ => Result.Success()
        };

        if (resultado.IsFailure)
        {
            _logger.LogWarning(
                "Transição {FullCode} recusada para o pedido {PedidoId}: {Erro}",
                fullCode, pedido.IdExterno, resultado.Error.Code);
        }
    }
}
