namespace DeliveryHub.Infrastructure.Persistence;

internal sealed class IFoodMerchantSnapshotEntity
{
    public Guid MerchantId { get; set; }
    public string DetailsJson { get; set; } = string.Empty;
    public string StatusJson { get; set; } = string.Empty;
    public string OpeningHoursJson { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}