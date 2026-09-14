using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Orders;
using DeliveryHub.Domain.SharedKernel;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DeliveryHub.Api.Diagnostics;

// Gerador de pedido local para "Validação viva" (CLAUDE.md §2, passo 3).
// Existe porque o sandbox do iFood entrega endereço sintético ("Rua TESTE") com
// coordenada 0,0 — impossível validar qualquer coisa geográfica com isso. Aqui
// o endereço é real e geocodificado, então dá para conferir mapa e rota.
//
// Só sobe em Development: o registro em Program.cs é condicional.
public static class PedidoLocalEndpoints
{
    // Praça André Nunes, Vila das Mercês — coordenadas do Nominatim (OSM).
    private static readonly Endereco EnderecoDeEntrega = new(
        Logradouro: "Praça André Nunes",
        Numero: "80",
        Bairro: "Vila das Mercês",
        Cidade: "São Paulo",
        Estado: "SP",
        Cep: "04165-160",
        Complemento: null,
        Referencia: null,
        Latitude: -23.6307077,
        Longitude: -46.6045437);

    public static void MapPedidoLocalEndpoints(this IEndpointRouteBuilder app)
    {
        // Sob /api de propósito: é o único prefixo que o proxy do Vite
        // encaminha, então o painel consegue chamar isso direto do navegador.
        app.MapPost("/api/diagnostics/pedido-local", Gerar)
            .WithTags("Diagnostics")
            .RequireAuthorization(Identity.Policies.OperadorDaLoja);
    }

    private static async Task<Results<Ok<object>, ProblemHttpResult>> Gerar(
        ITenantContext tenant,
        IPedidoRepository pedidos,
        INotificadorPainel notificador,
        TimeProvider relogio,
        CancellationToken ct,
        // Pedido marcado como teste nunca gera repasse ao entregador (§7), o
        // que torna impossível validar a tela de ganhos. Passar false cria um
        // pedido que percorre o fluxo financeiro inteiro — e só existe em
        // Development, então não há risco de virar lançamento de verdade.
        bool ehTeste = true)
    {
        if (tenant.MerchantId is not { } merchantId)
        {
            return TypedResults.Problem(
                title: "Sem loja no token.",
                detail: "diagnostics.sem_merchant",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var agora = relogio.GetUtcNow();

        var pedido = Pedido.Receber(
            merchantId: merchantId,
            // Prefixo "local-" separa do id do iFood: a ingestão deduplica por
            // este campo, e um id local nunca pode colidir com um de lá.
            idExterno: $"local-{Guid.CreateVersion7()}",
            numeroExibicao: Random.Shared.Next(1000, 9999).ToString(),
            ehTeste: ehTeste,
            cliente: new Cliente("Cliente Local de Teste", null, "LOCAL"),
            enderecoEntrega: EnderecoDeEntrega,
            valorTotal: 74.90m,
            taxaEntrega: 7.90m,
            criadoNaOrigemEm: agora,
            recebidoEm: agora);

        pedido.AdicionarItem(1, "Pizza Marguerita Grande", 1, "UN", 59.90m, 59.90m, "Sem cebola");
        pedido.AdicionarItem(2, "Refrigerante 2L", 1, "UN", 15.00m, 15.00m, null);

        pedidos.Adicionar(pedido);
        await pedidos.SalvarAsync(ct);

        // Pedido novo muda receita e contagem do dia: sem este aviso o painel
        // só perceberia no poll seguinte.
        await notificador.ResumoAtualizadoAsync(merchantId, ct);

        return TypedResults.Ok<object>(new
        {
            pedidoId = pedido.Id,
            numeroExibicao = pedido.NumeroExibicao,
            status = pedido.Status.ToString(),
            ehTeste = pedido.EhTeste,
            endereco = $"{EnderecoDeEntrega.Logradouro}, {EnderecoDeEntrega.Numero} - {EnderecoDeEntrega.Bairro}",
            latitude = EnderecoDeEntrega.Latitude,
            longitude = EnderecoDeEntrega.Longitude
        });
    }
}
