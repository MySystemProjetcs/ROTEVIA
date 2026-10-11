using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Merchants;
using DeliveryHub.Domain.SharedKernel;

namespace DeliveryHub.Application.Merchants;

public sealed record ImpressoraInput(
    string Nome,
    ModeloImpressora Modelo,
    LarguraPapel Largura,
    SetorDeImpressao Setor,
    bool ImprimirAutomaticamente);

public sealed record ImpressoraDto(
    Guid Id,
    string Nome,
    ModeloImpressora Modelo,
    LarguraPapel Largura,
    SetorDeImpressao Setor,
    bool ImprimirAutomaticamente,
    bool Ativa,
    DateTimeOffset CriadaEm);

public interface IListarImpressoras
{
    Task<IReadOnlyList<ImpressoraDto>> ExecutarAsync(Guid merchantId, CancellationToken ct);
}

public interface ICadastrarImpressora
{
    Task<Result<ImpressoraDto>> ExecutarAsync(Guid merchantId, ImpressoraInput input, CancellationToken ct);
}

public interface IAtualizarImpressora
{
    Task<Result<ImpressoraDto>> ExecutarAsync(Guid merchantId, Guid impressoraId, ImpressoraInput input, CancellationToken ct);
}

public interface IRemoverImpressora
{
    Task<Result> ExecutarAsync(Guid merchantId, Guid impressoraId, CancellationToken ct);
}

// Repositório da impressora: a entidade é pequena o suficiente para que o
// próprio caso de uso resolva o CRUD sem camada de serviço intermediária.
public interface IImpressoraRepository
{
    Task<ImpressoraDaLoja?> ObterAsync(Guid merchantId, Guid impressoraId, CancellationToken ct);
    Task<IReadOnlyList<ImpressoraDaLoja>> ListarAsync(Guid merchantId, CancellationToken ct);
    void Adicionar(ImpressoraDaLoja impressora);
    void Remover(ImpressoraDaLoja impressora);
    Task SalvarAsync(CancellationToken ct);
}

internal static class ImpressoraMappings
{
    public static ImpressoraDto ParaDto(this ImpressoraDaLoja x) => new(
        x.Id, x.Nome, x.Modelo, x.Largura, x.Setor,
        x.ImprimirAutomaticamente, x.Ativa, x.CriadaEm);
}

public sealed class ListarImpressoras : IListarImpressoras
{
    private readonly IImpressoraRepository _repo;

    public ListarImpressoras(IImpressoraRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<ImpressoraDto>> ExecutarAsync(Guid merchantId, CancellationToken ct)
    {
        var impressoras = await _repo.ListarAsync(merchantId, ct);
        return impressoras.Select(ImpressoraMappings.ParaDto).ToList();
    }
}

public sealed class CadastrarImpressora : ICadastrarImpressora
{
    private readonly IImpressoraRepository _repo;
    private readonly TimeProvider _clock;

    public CadastrarImpressora(IImpressoraRepository repo, TimeProvider clock)
    {
        _repo = repo;
        _clock = clock;
    }

    public async Task<Result<ImpressoraDto>> ExecutarAsync(Guid merchantId, ImpressoraInput input, CancellationToken ct)
    {
        var criada = ImpressoraDaLoja.Criar(
            merchantId, input.Nome, input.Modelo, input.Largura, input.Setor,
            input.ImprimirAutomaticamente, _clock.GetUtcNow());

        if (criada.IsFailure)
            return Result.Failure<ImpressoraDto>(criada.Error);

        _repo.Adicionar(criada.Value);
        await _repo.SalvarAsync(ct);
        return Result.Success(criada.Value.ParaDto());
    }
}

public sealed class AtualizarImpressora : IAtualizarImpressora
{
    private readonly IImpressoraRepository _repo;

    public AtualizarImpressora(IImpressoraRepository repo) => _repo = repo;

    public async Task<Result<ImpressoraDto>> ExecutarAsync(
        Guid merchantId, Guid impressoraId, ImpressoraInput input, CancellationToken ct)
    {
        var impressora = await _repo.ObterAsync(merchantId, impressoraId, ct);
        if (impressora is null)
            return Result.Failure<ImpressoraDto>(MerchantErrors.ImpressoraNaoEncontrada);

        var atualizacao = impressora.Atualizar(
            input.Nome, input.Modelo, input.Largura, input.Setor, input.ImprimirAutomaticamente);

        if (atualizacao.IsFailure)
            return Result.Failure<ImpressoraDto>(atualizacao.Error);

        await _repo.SalvarAsync(ct);
        return Result.Success(impressora.ParaDto());
    }
}

public sealed class RemoverImpressora : IRemoverImpressora
{
    private readonly IImpressoraRepository _repo;

    public RemoverImpressora(IImpressoraRepository repo) => _repo = repo;

    public async Task<Result> ExecutarAsync(Guid merchantId, Guid impressoraId, CancellationToken ct)
    {
        var impressora = await _repo.ObterAsync(merchantId, impressoraId, ct);
        if (impressora is null)
            return Result.Failure(MerchantErrors.ImpressoraNaoEncontrada);

        _repo.Remover(impressora);
        await _repo.SalvarAsync(ct);
        return Result.Success();
    }
}
