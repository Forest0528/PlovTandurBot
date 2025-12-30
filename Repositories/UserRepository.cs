using Google.Cloud.Firestore;
using PlovTandurBot.Models;

namespace PlovTandurBot.Repositories;

public class UserRepository
{
    private readonly FirestoreDb _db;
    private const string CollectionName = "users";

    public UserRepository(FirestoreDb db)
    {
        _db = db;
    }

    public async Task<UserProfile?> GetByIdAsync(long chatId, CancellationToken ct = default)
    {
        var docRef = _db.Collection(CollectionName).Document(chatId.ToString());
        var snapshot = await docRef.GetSnapshotAsync(ct);
        return snapshot.Exists ? snapshot.ConvertTo<UserProfile>() : null;
    }

    public async Task<UserProfile> GetOrCreateAsync(long chatId, string username = "", CancellationToken ct = default)
    {
        var user = await GetByIdAsync(chatId, ct);
        if (user != null)
        {
            user.LastActivity = DateTime.UtcNow;
            await UpdateAsync(user, ct);
            return user;
        }

        user = new UserProfile
        {
            ChatId = chatId,
            Username = username,
            CreatedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow
        };

        await CreateAsync(user, ct);
        return user;
    }

    public async Task CreateAsync(UserProfile user, CancellationToken ct = default)
    {
        var docRef = _db.Collection(CollectionName).Document(user.ChatId.ToString());
        await docRef.SetAsync(user, cancellationToken: ct);
    }

    public async Task UpdateAsync(UserProfile user, CancellationToken ct = default)
    {
        user.LastActivity = DateTime.UtcNow;
        var docRef = _db.Collection(CollectionName).Document(user.ChatId.ToString());
        await docRef.SetAsync(user, cancellationToken: ct);
    }

    public async Task<List<UserProfile>> GetByTypeAsync(UserType userType, CancellationToken ct = default)
    {
        var query = _db.Collection(CollectionName)
            .WhereEqualTo("UserType", userType.ToString());
        
        var snapshot = await query.GetSnapshotAsync(ct);
        return snapshot.Documents.Select(d => d.ConvertTo<UserProfile>()).ToList();
    }

    public async Task<List<UserProfile>> GetAllAsync(CancellationToken ct = default)
    {
        var snapshot = await _db.Collection(CollectionName).GetSnapshotAsync(ct);
        return snapshot.Documents.Select(d => d.ConvertTo<UserProfile>()).ToList();
    }

    public async Task<List<UserProfile>> GetActiveUsersAsync(CancellationToken ct = default)
    {
        var query = _db.Collection(CollectionName)
            .WhereEqualTo("IsBlocked", false);
        
        var snapshot = await query.GetSnapshotAsync(ct);
        return snapshot.Documents.Select(d => d.ConvertTo<UserProfile>()).ToList();
    }
}
