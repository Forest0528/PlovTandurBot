using Google.Cloud.Firestore;

namespace PlovTandurBot.Models;

public enum BroadcastStatus
{
    Draft,
    Testing,
    Sent
}

public enum TargetAudience
{
    All,
    Regular,
    VIP
}

[FirestoreData]
public class BroadcastMessage
{
    [FirestoreProperty]
    public string MessageId { get; set; } = Guid.NewGuid().ToString();

    [FirestoreProperty]
    public string Text { get; set; } = string.Empty;

    [FirestoreProperty]
    public string TargetAudience { get; set; } = Models.TargetAudience.All.ToString();

    [FirestoreProperty]
    public string Status { get; set; } = BroadcastStatus.Draft.ToString();

    [FirestoreProperty]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [FirestoreProperty]
    public DateTime? SentAt { get; set; }

    [FirestoreProperty]
    public int DeliveredCount { get; set; }

    [FirestoreProperty]
    public int BlockedCount { get; set; }

    [FirestoreProperty]
    public int FailedCount { get; set; }

    [FirestoreProperty]
    public long CreatedByAdminId { get; set; }

    // Helper properties
    [FirestoreProperty(Name = null)]
    public BroadcastStatus StatusEnum
    {
        get => Enum.Parse<BroadcastStatus>(Status);
        set => Status = value.ToString();
    }

    [FirestoreProperty(Name = null)]
    public TargetAudience TargetAudienceEnum
    {
        get => Enum.Parse<TargetAudience>(TargetAudience);
        set => TargetAudience = value.ToString();
    }
}
