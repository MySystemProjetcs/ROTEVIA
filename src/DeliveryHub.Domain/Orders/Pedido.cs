using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Domain.Orders;

public sealed class Pedido : ITenantOwned
{
    private readonly List<ItemPedido> _itens = [];

    // Usado só pelo EF Core: tipos owned (Cliente, Endereco) não podem ser
    // injetados por construtor, então a materialização passa por aqui.
    private Pedido() { }

    private Pedido(
        Guid id,
        Guid merchantId,
        string idExterno,
        string numeroExibicao,
        bool ehTeste,
        Cliente cliente,
        Endereco? enderecoEntrega,
        decimal valorTotal,
        decimal taxaEntrega,
        DateTimeOffset criadoNaOrigemEm,
        DateTimeOffset recebidoEm)
    {
        Id = id;
        MerchantId = merchantId;
        IdExterno = idExterno;
        NumeroExibicao = numeroExibicao;
        EhTeste = ehTeste;
        Cliente = cliente;
        EnderecoEntrega = enderecoEntrega;
        ValorTotal = valorTotal;
        TaxaEntrega = taxaEntrega;
        CriadoNaOrigemEm = criadoNaOrigemEm;
        RecebidoEm = recebidoEm;
        Status = StatusPedido.Recebido;
    }

    public Guid Id { get; }
    public Guid MerchantId { get; }

    // Id do pedido na origem. É por ele que a ingestão deduplica.
    public string IdExterno { get; }

    // Prefixo reservado a pedido nascido dentro do próprio sistema — PDV
    // próprio (CLAUDE.md §5) ou gerador de validação. Não colide com id de
    // marketplace, que é sempre GUID.
    public const string PrefixoOrigemLocal = "local-";

    // Pedido sem origem externa não tem marketplace a avisar: tentar confirmar
    // lá fora devolveria erro para uma venda que só existe aqui.
    public bool TemOrigemExterna =>
        !IdExterno.StartsWith(PrefixoOrigemLocal, StringComparison.Ordinal);

    // O número curto que o lojista vê e usa para falar do pedido.
    public string NumeroExibicao { get; }

    // Vem do isTest do payload do iFood. Precisa sobreviver até o Ledger:
    // pedido de sandbox não pode virar lançamento financeiro.
    public bool EhTeste { get; }

    public Cliente Cliente { get; private set; } = null!;

    // Nulo quando o pedido é retirada no balcão ou consumo no local.
    public Endereco? EnderecoEntrega { get; private set; }

    public decimal ValorTotal { get; }
    public decimal TaxaEntrega { get; }
    public DateTimeOffset CriadoNaOrigemEm { get; }
    public DateTimeOffset RecebidoEm { get; }
    public StatusPedido Status { get; private set; }

    // Aponta pro Courier.Id (identidade global do motoboy) — o próprio
    // MerchantId do pedido já resolve qual CourierMerchantLink é o relevante,
    // não precisa guardar o vínculo, só quem é o entregador.
    public Guid? EntregadorId { get; private set; }

    // Snapshot do quanto esse pedido pagou ao motoboy, gravado na conclusão
    // com a taxa vigente da loja. Nulo = não aplicável (sem entregador,
    // pedido de teste, ou concluído antes do recurso existir). Imutável depois
    // de gravado: reajuste de taxa nunca reescreve ganho passado.
    public decimal? ValorPagoAoEntregador { get; private set; }

    public IReadOnlyList<ItemPedido> Itens => _itens;

    public static Pedido Receber(
        Guid merchantId,
        string idExterno,
        string numeroExibicao,
        bool ehTeste,
        Cliente cliente,
        Endereco? enderecoEntrega,
        decimal valorTotal,
        decimal taxaEntrega,
        DateTimeOffset criadoNaOrigemEm,
        DateTimeOffset recebidoEm,
        Pagamento? pagamento = null,
        bool exigeCodigoDeEntrega = false,
        bool entregaPeloParceiro = false,
        string? codigoDeEntrega = null) =>
        new(Guid.CreateVersion7(), merchantId, idExterno, numeroExibicao, ehTeste, cliente,
            enderecoEntrega, valorTotal, taxaEntrega, criadoNaOrigemEm, recebidoEm)
        {
            Pagamento = pagamento ?? Pagamento.Indefinido,
            EntregaPeloParceiro = entregaPeloParceiro,
            ExigeCodigoDeEntrega = exigeCodigoDeEntrega,
            CodigoDeEntrega = codigoDeEntrega
        };

    // O item herda o tenant do pedido: é o pedido que sabe de quem ele é, e
    // deixar isso a cargo de quem chama seria abrir espaço para item órfão.
    public void AdicionarItem(int indice, string nome, int quantidade, string unidade,
        decimal precoUnitario, decimal precoTotal, string? observacoes) =>
        _itens.Add(ItemPedido.Criar(Id, MerchantId, indice, nome, quantidade, unidade, precoUnitario, precoTotal, observacoes));

    public Result Confirmar() => AvancarPara(StatusPedido.Confirmado);
    public Result IniciarPreparo() => AvancarPara(StatusPedido.EmPreparo);
    public Result MarcarPronto() => AvancarPara(StatusPedido.Pronto);

    // Só define quem vai entregar — não move o status. O pedido continua
    // "Pronto" até o dono clicar em Despachar de verdade.
    public Result AlocarEntregador(Guid entregadorId)
    {
        EntregadorId = entregadorId;
        return Result.Success();
    }

    public Result Despachar()
    {
        if (EntregadorId is null)
            return Result.Failure(PedidoErrors.SemEntregadorAlocado);

        return AvancarPara(StatusPedido.Despachado);
    }

    // Pedidos casados: quando vários saem juntos com o mesmo motoboy, cada um
    // guarda o id da corrida (LoteEntregaId) e sua posição na sequência de
    // paradas (OrdemNaRota, 1-based). Null nos dois = entrega solo. Quem calcula
    // a ordem é o caso de uso (DespacharEmLote), a partir das coordenadas — o
    // domínio só registra o resultado.
    public Guid? LoteEntregaId { get; private set; }
    public int? OrdemNaRota { get; private set; }

    public Result DefinirRotaDeLote(Guid loteId, int ordemNaRota)
    {
        LoteEntregaId = loteId;
        OrdemNaRota = ordemNaRota;
        return Result.Success();
    }

    // Passos do motoboy depois que o dono despacha. Nenhum deles avisa o
    // iFood — só o clique do dono em Despachar faz isso, como já fazia antes.
    public Result AceitarEntrega() => AvancarPara(StatusPedido.Aceito);
    public Result SairParaEntrega() => AvancarPara(StatusPedido.EmRota);
    public Result ChegarNoLocal() => AvancarPara(StatusPedido.Chegou);

    // Só faz sentido em pedido com pendência. Em pedido pago online cobrar
    // seria pedir dinheiro duas vezes ao cliente.
    public Result Cobrar()
    {
        if (!Pagamento.PrecisaCobrarNaEntrega)
            return Result.Failure(PedidoErrors.PedidoJaPago);

        var transicao = AvancarPara(StatusPedido.Cobrar);
        if (transicao.IsFailure)
            return transicao;

        // O que estava pendente virou dinheiro em mãos. É isto que faz o pedido
        // entrar na receita do dia, que só conta pedido sem pendência.
        Pagamento = Pagamento with
        {
            ValorJaPago = Pagamento.ValorJaPago + Pagamento.ValorACobrar,
            ValorACobrar = 0,
        };

        return Result.Success();
    }

    public Result Concluir() => AvancarPara(StatusPedido.Concluido);

    // O pedido do iFood entregue pela frota da própria loja pede um código de
    // 4 dígitos que o cliente informa na porta. Validado, o cliente perde o
    // direito de pedir cancelamento por "pedido não entregue" — é esse
    // prejuízo que o código evita.
    public bool ExigeCodigoDeEntrega { get; private set; }

    // Código de 4 dígitos que o iFood envia junto do pedido (campo pickupCode
    // do payload). O motoboy vê este número no card dele para confirmar na
    // porta do cliente — a confirmação em si bate contra o endpoint
    // verifyDeliveryCode do iFood (nunca contra este campo), que é a fonte da
    // verdade. Nulo quando o pedido não exige código (pedido interno, pedido
    // iFood entregue pela própria plataforma).
    public string? CodigoDeEntrega { get; private set; }

    // A frota do próprio marketplace faz a entrega (courier deles, não o
    // motoboy da loja). Conceito de negócio, não vocabulário de marketplace —
    // nasce do "deliveredBy" do iFood e do "delivery_type" da 99Food, mas o
    // domínio nunca vê esses nomes (CLAUDE.md §4). É o que decide se a loja
    // ainda tem uma etapa de entrega própria a percorrer depois do "Pronto".
    public bool EntregaPeloParceiro { get; private set; }

    public DateTimeOffset? CodigoConfirmadoEm { get; private set; }

    public bool CodigoDeEntregaPendente => ExigeCodigoDeEntrega && CodigoConfirmadoEm is null;

    // Idempotente: rede instável faz o motoboy tocar duas vezes, e a segunda
    // não pode reescrever a hora da primeira confirmação.
    public void RegistrarCodigoConfirmado(DateTimeOffset em)
    {
        CodigoConfirmadoEm ??= em;
    }

    // Porta separada do Concluir() de propósito. Concluir() tem duas entradas:
    // este fluxo e o evento CONCLUDED do iFood, que chega pelo polling.
    //
    // A guarda de código de entrega pendente foi removida temporariamente: em
    // teste não há código real do cliente para confirmar, e ela travava a
    // finalização pelo motoboy. Volta quando houver pedido real com código —
    // a capacidade de confirmar (ConfirmarEntregaComCodigo + RegistrarCodigoConfirmado)
    // continua disponível.
    public Result ConcluirPeloEntregador()
    {
        return Concluir();
    }

    // Como o pedido foi pago, e quanto ainda falta receber. Nunca nulo: pedido
    // sem informação de pagamento vale como pago (Pagamento.Indefinido), para
    // não inventar cobrança onde não há dado.
    public Pagamento Pagamento { get; private set; } = Pagamento.Indefinido;

    // Por que o pedido foi cancelado e quando. Motivo é opcional aqui de
    // propósito: o cancelamento vindo do próprio iFood (evento CANCELLED) nem
    // sempre traz texto. A obrigatoriedade do motivo no cancelamento pelo dono
    // é regra do caso de uso (CancelarPedido), não do domínio.
    public string? MotivoCancelamento { get; private set; }
    public DateTimeOffset? CanceladoEm { get; private set; }

    // Alcançável de qualquer estado não terminal (aguardando confirmação,
    // confirmado, em preparo, pronto, despachado/aguardando aceite, em rota...).
    // Concluído é a única barreira: pedido entregue não volta atrás.
    public Result Cancelar(string? motivo = null, DateTimeOffset? em = null)
    {
        if (Status == StatusPedido.Cancelado)
            return Result.Success();

        if (Status == StatusPedido.Concluido)
            return Result.Failure(PedidoErrors.PedidoConcluido);

        Status = StatusPedido.Cancelado;
        MotivoCancelamento = motivo;
        CanceladoEm = em;
        return Result.Success();
    }

    // Registra o ganho dessa entrega. Chamada uma única vez, na conclusão,
    // por quem conhece a taxa vigente (o caso de uso, não o domínio).
    // Segunda chamada é no-op de sucesso: reentrega de webhook não duplica.
    public Result RegistrarRepasseAoEntregador(decimal valor)
    {
        if (ValorPagoAoEntregador.HasValue)
            return Result.Success();

        if (valor < 0)
            return Result.Failure(PedidoErrors.ValorRepasseInvalido);

        ValorPagoAoEntregador = valor;
        return Result.Success();
    }

    private Result AvancarPara(StatusPedido destino)
    {
        if (Status == StatusPedido.Cancelado)
            return Result.Failure(PedidoErrors.PedidoCancelado);

        if (Status == StatusPedido.Concluido)
            return destino == StatusPedido.Concluido
                ? Result.Success()
                : Result.Failure(PedidoErrors.PedidoConcluido);

        // Progressão monotônica: evento atrasado ou repetido é no-op de sucesso,
        // nunca erro e nunca retrocesso. O polling não garante ordem, então um
        // DSP chegando depois de CON não pode desfazer a conclusão.
        if (destino <= Status)
            return Result.Success();

        Status = destino;
        return Result.Success();
    }
}
