using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Domain.Merchants;

// O tenant. É por IFoodMerchantId que a ingestão traduz o merchantId que vem
// no evento do iFood para o nosso MerchantId — sem isso o pedido não tem dono.
//
// IFoodMerchantId nasce nulo: no fluxo Distribuído (app de terceiro, N
// restaurantes) esse id só existe depois que o próprio dono da loja autoriza
// pelo Portal do Parceiro — pedir no cadastro seria inventar um dado que ainda
// não existe naquele momento.
public sealed class Merchant
{
    private Merchant() { }

    public Guid Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public Guid? IFoodMerchantId { get; private set; }

    // Identificador da loja no cadastro da 99Food. Não é Guid: o guia de
    // integração é explícito que quem escolhe o formato é o próprio
    // integrador ("números, códigos, letras, o que for melhor") — nasce nulo
    // até a loja ser configurada lá, e vive isolado do IFoodMerchantId acima:
    // são identidades de marketplaces diferentes, sem relação entre si.
    public string? NoventaENoveAppShopId { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }

    // Quanto a loja paga ao motoboy por entrega concluída. É o valor vigente —
    // o que vale pro histórico é o snapshot gravado no pedido na conclusão,
    // nunca este campo (reajuste não reescreve ganho passado).
    public decimal TaxaPadraoPorEntrega { get; private set; }

    public ConexaoIFood? ConexaoIFood { get; private set; }
    public ConexaoWhatsApp? ConexaoWhatsApp { get; private set; }

    // Onde a loja fica de fato. É a origem de toda entrega: o mapa ancora nela
    // e a rota do motoboy parte dali. Nasce nulo porque o cadastro só exige o
    // nome — a loja pode existir antes de alguém informar o endereço.
    public Endereco? Endereco { get; private set; }

    public static Merchant Criar(string nome, DateTimeOffset criadoEm) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Nome = nome,
            CriadoEm = criadoEm
        };

    public void DefinirEndereco(Endereco endereco) => Endereco = endereco;

    // Sem handshake OAuth como o iFood: o app_shop_id da 99Food é escolhido
    // por nós na criação manual da loja no painel deles, então vincular aqui é
    // só registrar o valor combinado — não há token nem autorização a trocar.
    public Result VincularNoventaENove(string appShopId)
    {
        if (string.IsNullOrWhiteSpace(appShopId))
            return Result.Failure(MerchantErrors.AppShopIdInvalido);

        NoventaENoveAppShopId = appShopId.Trim();
        return Result.Success();
    }

    // Gera o par userCode/verifier a guardar até o dono da loja voltar com o
    // código de autorização. Reiniciar uma conexão pendente é permitido (o
    // código anterior só tinha 10 minutos de vida); reconectar loja já
    // conectada não é — evitaria perder o token válido por engano.
    public Result IniciarConexaoIFood(string userCode, string authorizationCodeVerifier, DateTimeOffset codigoExpiraEm)
    {
        if (ConexaoIFood?.Conectado == true)
            return Result.Failure(MerchantErrors.JaConectado);

        ConexaoIFood = new ConexaoIFood(userCode, authorizationCodeVerifier, codigoExpiraEm, null, null, null, null, null);
        return Result.Success();
    }

    public Result ConfirmarConexaoIFood(
        Guid ifoodMerchantId, string accessToken, string refreshToken, string tipoToken,
        DateTimeOffset tokenExpiraEm, DateTimeOffset agora)
    {
        if (ConexaoIFood is null)
            return Result.Failure(MerchantErrors.ConexaoNaoIniciada);

        if (ConexaoIFood.CodigoExpiraEm < agora)
            return Result.Failure(MerchantErrors.CodigoExpirado);

        IFoodMerchantId = ifoodMerchantId;

        // Limpa userCode/verifier: já foram trocados por token, guardá-los
        // depois disso só criaria a ilusão de que dá para reusar um código de
        // uso único.
        ConexaoIFood = ConexaoIFood with
        {
            UserCode = null,
            AuthorizationCodeVerifier = null,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TipoToken = tipoToken,
            TokenExpiraEm = tokenExpiraEm,
            ConectadoEm = agora
        };

        return Result.Success();
    }

    public void AtualizarTokens(string accessToken, string refreshToken, string tipoToken, DateTimeOffset tokenExpiraEm)
    {
        if (ConexaoIFood is null)
            throw new InvalidOperationException("Não é possível renovar token de loja sem conexão iFood.");

        ConexaoIFood = ConexaoIFood with
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TipoToken = tipoToken,
            TokenExpiraEm = tokenExpiraEm
        };
    }

    // Sem Result: quem observa o worker do WhatsApp e reporta o que viu não
    // tem uma regra de negócio pra recusar — é constatação, não decisão.
    // Preserva o histórico: isto roda a cada webhook de status, e resetar a
    // lista aqui apagaria toda mensagem já registrada.
    public void AtualizarStatusWhatsApp(StatusConexaoWhatsApp status, string? telefone, DateTimeOffset? conectadoEm)
    {
        var historico = ConexaoWhatsApp?.Historico ?? [];
        ConexaoWhatsApp = new ConexaoWhatsApp(status, telefone, conectadoEm, historico);
    }

    // Idempotente por ExternalId: reentrega de webhook (mensagem recebida) ou
    // retry de envio (mensagem enviada) não duplica o histórico.
    public void RegistrarMensagemWhatsApp(MensagemWhatsApp mensagem)
    {
        var historicoAtual = ConexaoWhatsApp?.Historico ?? [];
        if (historicoAtual.Any(m => m.ExternalId == mensagem.ExternalId))
            return;

        var novoHistorico = historicoAtual.Append(mensagem).ToList();

        ConexaoWhatsApp = ConexaoWhatsApp is null
            ? new ConexaoWhatsApp(StatusConexaoWhatsApp.Desconectado, null, null, novoHistorico)
            : ConexaoWhatsApp with { Historico = novoHistorico };
    }

    public Result DefinirTaxaPorEntrega(decimal valor)
    {
        if (valor < 0)
            return Result.Failure(MerchantErrors.TaxaInvalida);

        TaxaPadraoPorEntrega = valor;
        return Result.Success();
    }
}
