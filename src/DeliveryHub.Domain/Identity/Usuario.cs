using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Domain.Identity;

public enum PapelUsuario
{
    // Nós. Cria restaurantes e gera as credenciais dos donos.
    AdministradorSistema = 0,

    // Dono do restaurante. Enxerga apenas as lojas vinculadas a ele.
    DonoRestaurante = 1,

    // Motoboy. Conta própria, sem senha definida pelo restaurante (CLAUDE.md
    // §6/ENGINEERING-GUIDE §10) — nasce só quando confirma o convite. Vínculo com
    // loja(s) fica em CourierMerchantLink, não em usuario_merchants.
    Entregador = 2
}

public static class UsuarioErrors
{
    public static readonly Error FotoInvalida = new(
        "usuario.foto_invalida",
        "Envie uma imagem PNG, JPEG ou WebP.",
        ErrorType.Validation);

    public static readonly Error FotoGrande = new(
        "usuario.foto_grande",
        "Imagem muito grande. Envie uma foto menor.",
        ErrorType.Validation);
}

public sealed class Usuario
{
    private Usuario() { }

    // Só estes três: são os formatos que todo navegador codifica por canvas, e
    // aceitar SVG aqui seria aceitar script — a foto é renderizada em <img> no
    // painel de quem a recebe.
    private static readonly string[] PrefixosDeFotoAceitos =
    [
        "data:image/png;base64,",
        "data:image/jpeg;base64,",
        "data:image/webp;base64,",
    ];

    // ~512KB já decodificado. O navegador reduz para 256px antes de enviar, o
    // que dá algumas dezenas de KB; o teto existe para quem chama a API direto.
    private const int TamanhoMaximoEmBase64 = 700_000;

    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;

    // Só o hash. A senha em claro existe uma única vez, no momento em que o
    // administrador a gera, e nunca é persistida nem registrada em log.
    public string SenhaHash { get; private set; } = string.Empty;

    public string Nome { get; private set; } = string.Empty;
    public PapelUsuario Papel { get; private set; }

    // Senha gerada por terceiro é senha comprometida por definição: vale até a
    // primeira troca. Quando o envio por e-mail existir, este campo é o que
    // permite migrar para convite sem refazer o modelo.
    public bool DeveTrocarSenha { get; private set; }

    public bool Ativo { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }

    // A imagem inteira como data URL ("data:image/webp;base64,..."), do jeito
    // que o <img> consome. Nulo é o estado normal: ninguém é obrigado a ter
    // foto, e a interface cai nas iniciais.
    public string? FotoBase64 { get; private set; }

    public static Usuario Criar(string email, string senhaHash, string nome, PapelUsuario papel, DateTimeOffset criadoEm) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Email = email.Trim().ToLowerInvariant(),
            SenhaHash = senhaHash,
            Nome = nome,
            Papel = papel,
            DeveTrocarSenha = true,
            Ativo = true,
            CriadoEm = criadoEm
        };

    public void DefinirNovaSenha(string senhaHash)
    {
        SenhaHash = senhaHash;
        DeveTrocarSenha = false;
    }

    public void Desativar() => Ativo = false;

    // Valida no domínio, não no endpoint: o que entra aqui volta como <img src>
    // para o navegador de quem abre o perfil, então o formato é regra de
    // negócio, não detalhe de transporte.
    public Result DefinirFoto(string dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl))
            return Result.Failure(UsuarioErrors.FotoInvalida);

        if (dataUrl.Length > TamanhoMaximoEmBase64)
            return Result.Failure(UsuarioErrors.FotoGrande);

        if (!PrefixosDeFotoAceitos.Any(p => dataUrl.StartsWith(p, StringComparison.Ordinal)))
            return Result.Failure(UsuarioErrors.FotoInvalida);

        // Base64 quebrado vira <img> que não carrega e ninguém entende por quê.
        var conteudo = dataUrl[(dataUrl.IndexOf(',', StringComparison.Ordinal) + 1)..];
        if (conteudo.Length == 0 || !Convert.TryFromBase64String(conteudo, new byte[conteudo.Length], out _))
            return Result.Failure(UsuarioErrors.FotoInvalida);

        FotoBase64 = dataUrl;
        return Result.Success();
    }
}
