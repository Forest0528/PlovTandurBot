using Google.Cloud.Firestore;

namespace PlovTandurBot.Models;

public enum UserType
{
    Regular,
    VIP
}

[FirestoreData]
public class UserProfile
{
    [FirestoreProperty]
    public long ChatId { get; set; }

    [FirestoreProperty]
    public string Username { get; set; } = string.Empty;

    [FirestoreProperty]
    public string WalletAddress { get; set; } = string.Empty;

    [FirestoreProperty]
    public string UserType { get; set; } = Models.UserType.Regular.ToString();

    [FirestoreProperty]
    public string State { get; set; } = "Start";

    [FirestoreProperty]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [FirestoreProperty]
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;

    [FirestoreProperty]
    public bool IsBlocked { get; set; } = false;

    [FirestoreProperty]
    public string Language { get; set; } = "ru";

    [FirestoreProperty]
    public string TempPromoCode { get; set; } = string.Empty;

    // Helper property (not stored in Firestore)
    [FirestoreProperty(Name = null)]
    public UserType UserTypeEnum
    {
        get => Enum.Parse<UserType>(UserType);
        set => UserType = value.ToString();
    }
}
