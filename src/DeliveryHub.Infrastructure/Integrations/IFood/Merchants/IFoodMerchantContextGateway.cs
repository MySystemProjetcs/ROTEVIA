using DeliveryHub.Application.Abstractions;
using DeliveryHub.Domain.Merchants;

namespace DeliveryHub.Infrastructure.Integrations.IFood.Merchants;

internal sealed class IFoodMerchantContextGateway : IIFoodMerchantContextGateway, IIFoodOpeningHoursGateway
{
    private readonly IIFoodMerchantTokenProvider _tokenProvider;
    private readonly IFoodMerchantDataClient _client;

    public IFoodMerchantContextGateway(
        IIFoodMerchantTokenProvider tokenProvider,
        IFoodMerchantDataClient client)
    {
        _tokenProvider = tokenProvider;
        _client = client;
    }

    public async Task<IFoodMerchantData> ObterAsync(Merchant merchant, CancellationToken ct)
    {
        var merchantId = merchant.IFoodMerchantId
            ?? throw new InvalidOperationException("Loja sem vínculo com comerciante iFood.");
        var autorizacao = await _tokenProvider.ObterAsync(merchant, ct);

        var detalhesTask = _client.ObterDetalhesAsync(merchantId, autorizacao, ct);
        var statusTask = _client.ObterStatusAsync(merchantId, autorizacao, ct);
        var horariosTask = _client.ObterHorarioDeFuncionamentoAsync(merchantId, autorizacao, ct);

        await Task.WhenAll(detalhesTask, statusTask, horariosTask);

        return new IFoodMerchantData(
            detalhesTask.Result.GetRawText(),
            statusTask.Result.GetRawText(),
            horariosTask.Result.GetRawText());
    }

    public async Task<string> CriarAsync(Merchant merchant, IFoodOpeningHoursInput input, CancellationToken ct)
    {
        var merchantId = merchant.IFoodMerchantId
            ?? throw new InvalidOperationException("Loja sem vínculo com comerciante iFood.");
        var autorizacao = await _tokenProvider.ObterAsync(merchant, ct);
        var response = await _client.CriarHorarioDeFuncionamentoAsync(merchantId, input, autorizacao, ct);
        return response.GetRawText();
    }

    public async Task<string> ObterHorarioAsync(Merchant merchant, CancellationToken ct)
    {
        var merchantId = merchant.IFoodMerchantId
            ?? throw new InvalidOperationException("Loja sem vínculo com comerciante iFood.");
        var autorizacao = await _tokenProvider.ObterAsync(merchant, ct);
        var response = await _client.ObterHorarioDeFuncionamentoAsync(merchantId, autorizacao, ct);
        return response.GetRawText();
    }
}