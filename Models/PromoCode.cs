using Google.Cloud.Firestore;

namespace PlovTandurBot.Models;

public enum PromoCodeStatus
{
    New,
    Activated,
    Used
}

[FirestoreData]
public class PromoCode
{
    [FirestoreProperty]
    public string Code { get; set; } = string.Empty;

    [FirestoreProperty]
    public string ProductName { get; set; } = string.Empty;

    [FirestoreProperty]
    public string ProductDescription { get; set; } = string.Empty;

    [FirestoreProperty]
    public string Status { get; set; } = PromoCodeStatus.New.ToString();

    [FirestoreProperty]
    public string NftAddress { get; set; } = string.Empty;

    [FirestoreProperty]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [FirestoreProperty]
    public DateTime? ActivatedAt { get; set; }

    [FirestoreProperty]
    public DateTime? UsedAt { get; set; }

    [FirestoreProperty]
    public long UserId { get; set; }

    [FirestoreProperty]
    public string OrderId { get; set; } = string.Empty;

    // Helper property
    [FirestoreProperty(Name = null)]
    public PromoCodeStatus StatusEnum
    {
        get => Enum.Parse<PromoCodeStatus>(Status);
        set => Status = value.ToString();
    }
}
