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

public sealed class Usuario
{
    private Usuario() { }

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
}
