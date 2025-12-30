using Google.Cloud.Firestore;

namespace PlovTandurBot.Models;

public enum NftTokenStatus
{
    Minted,
    Active,
    Redeemed
}

[FirestoreData]
public class NftToken
{
    [FirestoreProperty]
    public string TokenId { get; set; } = string.Empty;

    [FirestoreProperty]
    public string NftAddress { get; set; } = string.Empty;

    [FirestoreProperty]
    public string OwnerAddress { get; set; } = string.Empty;

    [FirestoreProperty]
    public string PromoCodeId { get; set; } = string.Empty;

    [FirestoreProperty]
    public string ProductName { get; set; } = string.Empty;

    [FirestoreProperty]
    public string Status { get; set; } = NftTokenStatus.Minted.ToString();

    [FirestoreProperty]
    public DateTime MintedAt { get; set; } = DateTime.UtcNow;

    [FirestoreProperty]
    public DateTime? RedeemedAt { get; set; }

    [FirestoreProperty]
    public string MintTransactionHash { get; set; } = string.Empty;

    [FirestoreProperty]
    public string RedeemTransactionHash { get; set; } = string.Empty;

    // Helper property
    [FirestoreProperty(Name = null)]
    public NftTokenStatus StatusEnum
    {
        get => Enum.Parse<NftTokenStatus>(Status);
        set => Status = value.ToString();
    }
}
