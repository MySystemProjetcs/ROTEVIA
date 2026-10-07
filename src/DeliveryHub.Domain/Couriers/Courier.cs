using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Domain.Couriers;

// Identidade global do entregador, chave natural CPF (CLAUDE.md §6): o mesmo
// motoboy trabalha para vários restaurantes sem recadastro, então Courier não
// pertence a nenhum Merchant. Quem liga um Courier a uma loja é
// CourierMerchantLink.
public sealed class Courier
{
    private Courier() { }

    public Guid Id { get; private set; }
    public string Cpf { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;
    public string Telefone { get; private set; } = string.Empty;
    public string ModeloDaMoto { get; private set; } = string.Empty;
    public string Placa { get; private set; } = string.Empty;

    // Nulo até o motoboy confirmar o primeiro convite (ConfirmarConviteEntregador)
    // e escolher a própria senha. Um segundo convite de outro restaurante reusa
    // esta mesma conta em vez de pedir senha de novo.
    public Guid? UsuarioId { get; private set; }

    // Disponibilidade global do motoboy (vale pra todas as lojas). É o
    // interruptor manual Online/Offline — "Em Entrega" é derivado dos pedidos
    // e não passa por aqui.
    public bool DisponivelParaEntrega { get; private set; } = true;

    public DateTimeOffset CriadoEm { get; private set; }

    public static Courier Criar(
        string cpf, string nome, string telefone, string modeloDaMoto, string placa, DateTimeOffset criadoEm) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Cpf = cpf,
            Nome = nome,
            Telefone = telefone,
            ModeloDaMoto = modeloDaMoto,
            Placa = placa,
            CriadoEm = criadoEm
        };

    // Idempotente de propósito: o segundo restaurante que convida o mesmo CPF
    // encontra UsuarioId já preenchido e não deve sobrescrever a conta ativa.
    public void VincularUsuario(Guid usuarioId)
    {
        UsuarioId ??= usuarioId;
    }

    // Correção de cadastro pelo restaurante. O CPF fica de fora de propósito: é
    // a chave natural que dedupe o mesmo motoboy entre lojas (CLAUDE.md §6), e
    // trocá-lo criaria uma segunda identidade para a mesma pessoa.
    //
    // Como o Courier é global, editar aqui reflete em toda loja que tenha
    // vínculo com ele — é o preço de não duplicar cadastro.
    public Result AtualizarCadastro(string nome, string telefone, string modeloDaMoto, string placa)
    {
        var nomeLimpo = nome?.Trim() ?? string.Empty;
        var telefoneLimpo = telefone?.Trim() ?? string.Empty;
        var motoLimpa = modeloDaMoto?.Trim() ?? string.Empty;
        var placaLimpa = placa?.Trim().ToUpperInvariant() ?? string.Empty;

        if (nomeLimpo.Length == 0 || telefoneLimpo.Length == 0
            || motoLimpa.Length == 0 || placaLimpa.Length == 0)
        {
            return Result.Failure(CourierErrors.DadosInvalidos);
        }

        Nome = nomeLimpo;
        Telefone = telefoneLimpo;
        ModeloDaMoto = motoLimpa;
        Placa = placaLimpa;
        return Result.Success();
    }

    public void DefinirDisponibilidade(bool disponivel)
    {
        DisponivelParaEntrega = disponivel;
    }
}
