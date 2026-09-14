using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Domain.Tracking;

// Um ping de GPS do motoboy durante a entrega. É série temporal, não estado:
// nunca sofre UPDATE, só INSERT — a tabela é hypertable do TimescaleDB
// (CLAUDE.md §3) e a posição "atual" é simplesmente o ping mais recente.
//
// É ITenantOwned porque o restaurante só pode acompanhar quem está entregando
// para ele: sem o filtro, uma loja veria o motoboy a caminho de outra.
public sealed class PosicaoEntregador : ITenantOwned
{
    private PosicaoEntregador() { }

    public Guid Id { get; private set; }
    public Guid MerchantId { get; private set; }
    public Guid EntregadorId { get; private set; }
    public Guid PedidoId { get; private set; }

    public double Latitude { get; private set; }
    public double Longitude { get; private set; }

    // Raio de erro em metros que o navegador reporta. Guardado porque um ping
    // de 2km de precisão não pode mover o marcador como um de 5m.
    public double PrecisaoEmMetros { get; private set; }

    // Quando o aparelho capturou, não quando o servidor recebeu: a diferença
    // importa quando o motoboy passa por área sem sinal e os pings chegam
    // todos juntos depois.
    public DateTimeOffset CapturadoEm { get; private set; }
    public DateTimeOffset RecebidoEm { get; private set; }

    public static PosicaoEntregador Registrar(
        Guid merchantId,
        Guid entregadorId,
        Guid pedidoId,
        double latitude,
        double longitude,
        double precisaoEmMetros,
        DateTimeOffset capturadoEm,
        DateTimeOffset recebidoEm) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            MerchantId = merchantId,
            EntregadorId = entregadorId,
            PedidoId = pedidoId,
            Latitude = latitude,
            Longitude = longitude,
            PrecisaoEmMetros = precisaoEmMetros,
            CapturadoEm = capturadoEm,
            RecebidoEm = recebidoEm
        };
}
