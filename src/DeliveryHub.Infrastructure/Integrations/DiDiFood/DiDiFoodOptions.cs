namespace DeliveryHub.Infrastructure.Integrations.DiDiFood;

public sealed class DiDiFoodOptions
{
    public const string SectionName = "DiDiFood";

    // ID numérico do app cadastrado em openapi.didi-food.com
    public long AppId { get; init; }

    // Chave secreta do app — usada para gerar e verificar assinaturas MD5
    public string AppSecret { get; init; } = string.Empty;
}
