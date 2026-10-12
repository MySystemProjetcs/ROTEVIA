using System.Text.Json;

namespace DeliveryHub.Api.Merchants;

// Tradução do payload de status do iFood para o vocabulário do painel. Fica
// fora do arquivo de endpoints porque tem ramo demais para ficar escondida
// num método privado: um erro aqui faz o painel mentir sobre a loja estar
// aberta, que é o pior tipo de bug neste fluxo.
public static class StatusDaLojaIFood
{
    public const string Online = "online";
    public const string Offline = "offline";
    public const string Indisponivel = "indisponivel";

    public static string? Nome(string detailsJson)
    {
        using var documento = JsonDocument.Parse(detailsJson);
        return documento.RootElement.ValueKind == JsonValueKind.Object
            && documento.RootElement.TryGetProperty("name", out var nome)
            && nome.ValueKind == JsonValueKind.String
                ? nome.GetString()
                : null;
    }

    // O iFood devolve um array de operações. A que interessa ao painel é a de
    // entrega no canal do próprio iFood — uma loja pode estar aberta no salão
    // e fechada para delivery, e é o delivery que importa aqui.
    //
    // Quando há uma única operação de entrega, ela vale mesmo sem o canal
    // bater: loja com um canal só não precisa declarar qual é.
    public static string Disponibilidade(string statusJson)
    {
        using var documento = JsonDocument.Parse(statusJson);
        if (documento.RootElement.ValueKind != JsonValueKind.Array)
            return Indisponivel;

        var entregas = documento.RootElement
            .EnumerateArray()
            .Where(EhOperacaoDeEntrega)
            .ToList();

        if (entregas.Count == 0)
            return Indisponivel;

        var doCanalIFood = entregas.FirstOrDefault(EhCanalIFood);
        var escolhida = doCanalIFood.ValueKind == JsonValueKind.Object
            ? doCanalIFood
            : entregas.Count == 1
                ? entregas[0]
                : default(JsonElement);

        if (escolhida.ValueKind != JsonValueKind.Object
            || !escolhida.TryGetProperty("available", out var disponivel)
            || disponivel.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            return Indisponivel;

        return disponivel.GetBoolean() ? Online : Offline;
    }

    private static bool EhOperacaoDeEntrega(JsonElement item) =>
        item.ValueKind == JsonValueKind.Object
        && item.TryGetProperty("operation", out var operacao)
        && operacao.ValueKind == JsonValueKind.String
        && string.Equals(operacao.GetString(), "DELIVERY", StringComparison.OrdinalIgnoreCase);

    private static bool EhCanalIFood(JsonElement item) =>
        item.TryGetProperty("salesChannel", out var canal)
        && canal.ValueKind == JsonValueKind.String
        && string.Equals(canal.GetString(), "IFOOD", StringComparison.OrdinalIgnoreCase);
}
