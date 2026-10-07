namespace DeliveryHub.Application.Orders;

// Ordena as paradas de uma corrida por vizinho-mais-próximo a partir da loja:
// da origem, vai sempre para a parada não visitada mais perto, e repete. É uma
// heurística (distância em linha reta, não rua real) — suficiente para poucas
// paradas, que é o caso dos pedidos casados. Trocar por roteirização real
// (OSRM) no futuro é só substituir esta peça (CLAUDE.md §9, opção B).
//
// Parada sem coordenada não entra no cálculo: vai para o fim, na ordem de
// entrada — melhor um destino sem otimização do que descartá-lo.
public readonly record struct ParadaDaRota(Guid PedidoId, double? Latitude, double? Longitude);

public static class SequenciadorDeRota
{
    public static IReadOnlyList<Guid> Ordenar(
        double origemLatitude, double origemLongitude, IReadOnlyList<ParadaDaRota> paradas)
    {
        var comCoordenada = paradas.Where(TemCoordenada).ToList();
        var semCoordenada = paradas.Where(p => !TemCoordenada(p)).Select(p => p.PedidoId).ToList();

        var ordenadas = new List<Guid>(comCoordenada.Count);
        var atualLat = origemLatitude;
        var atualLng = origemLongitude;

        while (comCoordenada.Count > 0)
        {
            var proxima = comCoordenada[0];
            var menorDistancia = Distancia(atualLat, atualLng, proxima.Latitude!.Value, proxima.Longitude!.Value);

            foreach (var candidata in comCoordenada.Skip(1))
            {
                var d = Distancia(atualLat, atualLng, candidata.Latitude!.Value, candidata.Longitude!.Value);
                if (d < menorDistancia)
                {
                    menorDistancia = d;
                    proxima = candidata;
                }
            }

            ordenadas.Add(proxima.PedidoId);
            atualLat = proxima.Latitude!.Value;
            atualLng = proxima.Longitude!.Value;
            comCoordenada.Remove(proxima);
        }

        ordenadas.AddRange(semCoordenada);
        return ordenadas;
    }

    private static bool TemCoordenada(ParadaDaRota p) => p.Latitude is not null && p.Longitude is not null;

    // Haversine — distância em km na esfera. Só a ordem relativa importa aqui,
    // então o raio da Terra exato é irrelevante, mas mantemos km por clareza.
    private static double Distancia(double lat1, double lng1, double lat2, double lng2)
    {
        const double raioTerraKm = 6371.0;
        var dLat = GrausParaRad(lat2 - lat1);
        var dLng = GrausParaRad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(GrausParaRad(lat1)) * Math.Cos(GrausParaRad(lat2))
            * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return raioTerraKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double GrausParaRad(double graus) => graus * Math.PI / 180.0;
}
