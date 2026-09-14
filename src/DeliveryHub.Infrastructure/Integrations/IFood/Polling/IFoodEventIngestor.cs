using System.Net.Http.Headers;
using System.Text.Json;
using DeliveryHub.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Polling;

public sealed record ResultadoIngestao(int Recebidos, int Gravados, int EmQuarentena, int Duplicados);

public interface IIFoodEventIngestor
{
    // autorizacao nulo faz o polling Centralizado (todas as lojas daquele
    // token); com token de uma loja específica, faz o polling só dela — é
    // assim que o worker itera pelas lojas do fluxo Distribuído.
    Task<ResultadoIngestao> IngerirAsync(CancellationToken ct, AuthenticationHeaderValue? autorizacao = null);
}

internal sealed class IFoodEventIngestor : IIFoodEventIngestor
{
    private const string Origem = "ifood";

    private readonly IIFoodEventsClient _events;
    private readonly IIntegrationInboxWriter _inbox;
    private readonly IMerchantResolver _merchants;
    private readonly ILogger<IFoodEventIngestor> _logger;

    public IFoodEventIngestor(
        IIFoodEventsClient events,
        IIntegrationInboxWriter inbox,
        IMerchantResolver merchants,
        ILogger<IFoodEventIngestor> logger)
    {
        _events = events;
        _inbox = inbox;
        _merchants = merchants;
        _logger = logger;
    }

    public async Task<ResultadoIngestao> IngerirAsync(CancellationToken ct, AuthenticationHeaderValue? autorizacao = null)
    {
        var eventos = await _events.PollAsync(ct, autorizacao);
        if (eventos.Count == 0)
            return new ResultadoIngestao(0, 0, 0, 0);

        var paraReconhecer = new List<Guid>(eventos.Count);
        var gravados = 0;
        var quarentena = 0;
        var duplicados = 0;

        foreach (var evento in eventos)
        {
            var merchantId = await _merchants.ResolverAsync(evento.MerchantId, ct);

            var novo = await _inbox.TentarGravarAsync(
                Origem,
                evento.Id.ToString(),
                evento.MerchantId,
                merchantId,
                JsonSerializer.Serialize(evento),
                ct);

            if (novo)
            {
                gravados++;
                if (merchantId is null)
                {
                    quarentena++;
                    // Loja autorizada no iFood mas ainda não cadastrada aqui.
                    // Não pode virar pedido órfão nem sumir em silêncio.
                    _logger.LogWarning(
                        "Evento em quarentena: merchant {MerchantIdExterno} desconhecido.",
                        evento.MerchantId);
                }
            }
            else
            {
                duplicados++;
            }

            // Só entra na lista de ack depois de estar no inbox. Reconhecer
            // antes de persistir perde o pedido se o processo cair aqui.
            paraReconhecer.Add(evento.Id);
        }

        await _events.AcknowledgeAsync(paraReconhecer, ct, autorizacao);

        return new ResultadoIngestao(eventos.Count, gravados, quarentena, duplicados);
    }
}
