using Google.Cloud.Firestore;
using PlovTandurBot.Models;

namespace PlovTandurBot.Repositories;

public class PromoCodeRepository
{
    private readonly FirestoreDb _db;
    private const string CollectionName = "promocodes";

    public PromoCodeRepository(FirestoreDb db)
    {
        _db = db;
    }

    public async Task<PromoCode?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var docRef = _db.Collection(CollectionName).Document(code.ToUpper());
        var snapshot = await docRef.GetSnapshotAsync(ct);
        return snapshot.Exists ? snapshot.ConvertTo<PromoCode>() : null;
    }

    public async Task CreateAsync(PromoCode promoCode, CancellationToken ct = default)
    {
        promoCode.Code = promoCode.Code.ToUpper();
        var docRef = _db.Collection(CollectionName).Document(promoCode.Code);
        await docRef.SetAsync(promoCode, cancellationToken: ct);
    }

    public async Task UpdateAsync(PromoCode promoCode, CancellationToken ct = default)
    {
        var docRef = _db.Collection(CollectionName).Document(promoCode.Code);
        await docRef.SetAsync(promoCode, cancellationToken: ct);
    }

    public async Task<bool> ActivateAsync(string code, long userId, CancellationToken ct = default)
    {
        var promoCode = await GetByCodeAsync(code, ct);
        if (promoCode == null || promoCode.StatusEnum != PromoCodeStatus.New)
            return false;

        promoCode.StatusEnum = PromoCodeStatus.Activated;
        promoCode.ActivatedAt = DateTime.UtcNow;
        promoCode.UserId = userId;

        await UpdateAsync(promoCode, ct);
        return true;
    }

    public async Task<bool> MarkAsUsedAsync(string code, string nftAddress, CancellationToken ct = default)
    {
        var promoCode = await GetByCodeAsync(code, ct);
        if (promoCode == null || promoCode.StatusEnum != PromoCodeStatus.Activated)
            return false;

        promoCode.StatusEnum = PromoCodeStatus.Used;
        promoCode.UsedAt = DateTime.UtcNow;
        promoCode.NftAddress = nftAddress;

        await UpdateAsync(promoCode, ct);
        return true;
    }

    public async Task<List<PromoCode>> GetByStatusAsync(PromoCodeStatus status, CancellationToken ct = default)
    {
        var query = _db.Collection(CollectionName)
            .WhereEqualTo("Status", status.ToString());
        
        var snapshot = await query.GetSnapshotAsync(ct);
        return snapshot.Documents.Select(d => d.ConvertTo<PromoCode>()).ToList();
    }

    public async Task<List<PromoCode>> GetByUserIdAsync(long userId, CancellationToken ct = default)
    {
        var query = _db.Collection(CollectionName)
            .WhereEqualTo("UserId", userId);
        
        var snapshot = await query.GetSnapshotAsync(ct);
        return snapshot.Documents.Select(d => d.ConvertTo<PromoCode>()).ToList();
    }
}
