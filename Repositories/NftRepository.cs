using Google.Cloud.Firestore;
using PlovTandurBot.Models;

namespace PlovTandurBot.Repositories;

public class NftRepository
{
    private readonly FirestoreDb _db;
    private const string CollectionName = "nfts";

    public NftRepository(FirestoreDb db)
    {
        _db = db;
    }

    public async Task<NftToken?> GetByAddressAsync(string nftAddress, CancellationToken ct = default)
    {
        var query = _db.Collection(CollectionName)
            .WhereEqualTo("NftAddress", nftAddress)
            .Limit(1);
        
        var snapshot = await query.GetSnapshotAsync(ct);
        return snapshot.Documents.FirstOrDefault()?.ConvertTo<NftToken>();
    }

    public async Task<NftToken?> GetByTokenIdAsync(string tokenId, CancellationToken ct = default)
    {
        var docRef = _db.Collection(CollectionName).Document(tokenId);
        var snapshot = await docRef.GetSnapshotAsync(ct);
        return snapshot.Exists ? snapshot.ConvertTo<NftToken>() : null;
    }

    public async Task CreateAsync(NftToken nft, CancellationToken ct = default)
    {
        var docRef = _db.Collection(CollectionName).Document(nft.TokenId);
        await docRef.SetAsync(nft, cancellationToken: ct);
    }

    public async Task UpdateAsync(NftToken nft, CancellationToken ct = default)
    {
        var docRef = _db.Collection(CollectionName).Document(nft.TokenId);
        await docRef.SetAsync(nft, cancellationToken: ct);
    }

    public async Task<bool> RedeemAsync(string nftAddress, string transactionHash, CancellationToken ct = default)
    {
        var nft = await GetByAddressAsync(nftAddress, ct);
        if (nft == null || nft.StatusEnum == NftTokenStatus.Redeemed)
            return false;

        nft.StatusEnum = NftTokenStatus.Redeemed;
        nft.RedeemedAt = DateTime.UtcNow;
        nft.RedeemTransactionHash = transactionHash;

        await UpdateAsync(nft, ct);
        return true;
    }

    public async Task<List<NftToken>> GetByOwnerAsync(string ownerAddress, CancellationToken ct = default)
    {
        var query = _db.Collection(CollectionName)
            .WhereEqualTo("OwnerAddress", ownerAddress);
        
        var snapshot = await query.GetSnapshotAsync(ct);
        return snapshot.Documents.Select(d => d.ConvertTo<NftToken>()).ToList();
    }

    public async Task<List<NftToken>> GetByStatusAsync(NftTokenStatus status, CancellationToken ct = default)
    {
        var query = _db.Collection(CollectionName)
            .WhereEqualTo("Status", status.ToString());
        
        var snapshot = await query.GetSnapshotAsync(ct);
        return snapshot.Documents.Select(d => d.ConvertTo<NftToken>()).ToList();
    }

    public async Task<List<NftToken>> GetByPromoCodeAsync(string promoCodeId, CancellationToken ct = default)
    {
        var query = _db.Collection(CollectionName)
            .WhereEqualTo("PromoCodeId", promoCodeId);
        
        var snapshot = await query.GetSnapshotAsync(ct);
        return snapshot.Documents.Select(d => d.ConvertTo<NftToken>()).ToList();
    }
}
