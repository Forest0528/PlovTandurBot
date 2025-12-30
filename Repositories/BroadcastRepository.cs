using Google.Cloud.Firestore;
using PlovTandurBot.Models;

namespace PlovTandurBot.Repositories;

public class BroadcastRepository
{
    private readonly FirestoreDb _db;
    private const string CollectionName = "broadcasts";

    public BroadcastRepository(FirestoreDb db)
    {
        _db = db;
    }

    public async Task<BroadcastMessage?> GetByIdAsync(string messageId, CancellationToken ct = default)
    {
        var docRef = _db.Collection(CollectionName).Document(messageId);
        var snapshot = await docRef.GetSnapshotAsync(ct);
        return snapshot.Exists ? snapshot.ConvertTo<BroadcastMessage>() : null;
    }

    public async Task CreateAsync(BroadcastMessage broadcast, CancellationToken ct = default)
    {
        var docRef = _db.Collection(CollectionName).Document(broadcast.MessageId);
        await docRef.SetAsync(broadcast, cancellationToken: ct);
    }

    public async Task UpdateAsync(BroadcastMessage broadcast, CancellationToken ct = default)
    {
        var docRef = _db.Collection(CollectionName).Document(broadcast.MessageId);
        await docRef.SetAsync(broadcast, cancellationToken: ct);
    }

    public async Task<List<BroadcastMessage>> GetByStatusAsync(BroadcastStatus status, CancellationToken ct = default)
    {
        var query = _db.Collection(CollectionName)
            .WhereEqualTo("Status", status.ToString())
            .OrderByDescending("CreatedAt");
        
        var snapshot = await query.GetSnapshotAsync(ct);
        return snapshot.Documents.Select(d => d.ConvertTo<BroadcastMessage>()).ToList();
    }

    public async Task<List<BroadcastMessage>> GetRecentAsync(int limit = 10, CancellationToken ct = default)
    {
        var query = _db.Collection(CollectionName)
            .OrderByDescending("CreatedAt")
            .Limit(limit);
        
        var snapshot = await query.GetSnapshotAsync(ct);
        return snapshot.Documents.Select(d => d.ConvertTo<BroadcastMessage>()).ToList();
    }
}
