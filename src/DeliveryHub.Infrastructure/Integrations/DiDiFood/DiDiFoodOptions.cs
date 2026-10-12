using System.ComponentModel.DataAnnotations;

namespace DeliveryHub.Infrastructure.Integrations.DiDiFood;

public sealed class DiDiFoodOptions
{
    public const string SectionName = "DiDiFood";

    // ID numérico do app cadastrado em openapi.didi-food.com
    [Range(1, long.MaxValue, ErrorMessage = "DiDiFood:AppId precisa ser um inteiro positivo.")]
    public long AppId { get; init; }

    // Chave secreta do app — usada para gerar e verificar assinaturas MD5
    [Required(AllowEmptyStrings = false, ErrorMessage = "DiDiFood:AppSecret não configurado.")]
    public string AppSecret { get; init; } = string.Empty;
}
