namespace DeliveryHub.Application.Couriers;

// Intervalo escolhido pelo motoboy, em datas do fuso da loja — não instantes.
// "01/09 a 14/09" significa da meia-noite do dia 1 até o fim do dia 14 em São
// Paulo; converter para UTC é responsabilidade de quem consulta o banco.
public sealed record IntervaloDeGanhos(DateOnly Inicio, DateOnly Fim);

public sealed record ItemGanho(
    Guid PedidoId,
    string NumeroExibicao,
    string NomeLoja,
    decimal Valor,
    DateTimeOffset RecebidoEm,
    // Contexto da entrega que gerou o ganho: sem isso o motoboy vê só um
    // número de pedido e não consegue conferir a que corrida se refere.
    string ClienteNome,
    string? EnderecoResumido,
    IReadOnlyList<string> Itens);

public sealed record ResultadoGanhos(
    // Total e quantidade são sempre do período inteiro, nunca da página: o
    // motoboy quer saber quanto ganhou no mês, não quanto há nesta tela.
    decimal Total,
    int QtdEntregas,
    IReadOnlyList<ItemGanho> Itens,
    int Pagina,
    int TamanhoPagina);

public interface IObterGanhosEntregador
{
    Task<ResultadoGanhos> ExecutarAsync(
        Guid usuarioId,
        IntervaloDeGanhos intervalo,
        int pagina,
        CancellationToken ct);
}
