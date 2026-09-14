namespace DeliveryHub.Application.Dashboard;

public sealed record ResumoDashboard(
    int MotoboysOnline,
    int MotoboysEmEntrega,
    int QtdPedidosHoje,
    decimal ReceitaHoje,
    decimal TaxaPorEntrega,
    // Quantos dos pedidos do dia são de teste. A receita os inclui, então a
    // tela precisa poder avisar que aquele número não é venda de verdade.
    int QtdDeTeste);

public interface IObterResumoDashboard
{
    Task<ResumoDashboard> ExecutarAsync(Guid merchantId, CancellationToken ct);
}
