using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Domain.Merchants;

public enum ModeloImpressora
{
    // Qualquer impressora que fale ESC/POS. Fallback universal — se não for
    // de nenhum fabricante listado, cai aqui.
    EscPosGenerica = 0,
    ElginI7 = 1,
    ElginI8 = 2,
    ElginI9 = 3,
    EpsonTmT20 = 10,
    BematechMp4200 = 20,
    DarumaDr800 = 30,
}

public enum LarguraPapel
{
    // 80mm é o padrão para delivery (comanda com endereço e observações).
    // 58mm cabe só o essencial, usado em alguns PDVs portáteis.
    Oitenta = 80,
    CinquentaEOito = 58,
}

public enum SetorDeImpressao
{
    Cozinha = 0,
    Balcao = 1,
    Bar = 2,
    Expedicao = 3,
}

// Uma impressora cadastrada pela loja. O payload da comanda vai do backend
// até a estação por SignalR; a estação (web + agente local) converte em
// ESC/POS e envia ao hardware. O backend não conhece IP de impressora na
// rede local — isso é dado da estação, não da nuvem.
public sealed class ImpressoraDaLoja
{
    private ImpressoraDaLoja() { }

    public Guid Id { get; private set; }
    public Guid MerchantId { get; private set; }
    public string Nome { get; private set; } = default!;
    public ModeloImpressora Modelo { get; private set; }
    public LarguraPapel Largura { get; private set; }
    public SetorDeImpressao Setor { get; private set; }
    public bool ImprimirAutomaticamente { get; private set; }
    public bool Ativa { get; private set; }
    public DateTimeOffset CriadaEm { get; private set; }

    public static Result<ImpressoraDaLoja> Criar(
        Guid merchantId,
        string nome,
        ModeloImpressora modelo,
        LarguraPapel largura,
        SetorDeImpressao setor,
        bool imprimirAutomaticamente,
        DateTimeOffset agora)
    {
        var nomeNormalizado = (nome ?? string.Empty).Trim();
        if (nomeNormalizado.Length is 0 or > 60)
            return Result.Failure<ImpressoraDaLoja>(MerchantErrors.ImpressoraNomeInvalido);

        return Result.Success(new ImpressoraDaLoja
        {
            Id = Guid.CreateVersion7(),
            MerchantId = merchantId,
            Nome = nomeNormalizado,
            Modelo = modelo,
            Largura = largura,
            Setor = setor,
            ImprimirAutomaticamente = imprimirAutomaticamente,
            Ativa = true,
            CriadaEm = agora,
        });
    }

    public Result Atualizar(
        string nome,
        ModeloImpressora modelo,
        LarguraPapel largura,
        SetorDeImpressao setor,
        bool imprimirAutomaticamente)
    {
        var nomeNormalizado = (nome ?? string.Empty).Trim();
        if (nomeNormalizado.Length is 0 or > 60)
            return Result.Failure(MerchantErrors.ImpressoraNomeInvalido);

        Nome = nomeNormalizado;
        Modelo = modelo;
        Largura = largura;
        Setor = setor;
        ImprimirAutomaticamente = imprimirAutomaticamente;
        return Result.Success();
    }

    public void Desativar() => Ativa = false;
}
