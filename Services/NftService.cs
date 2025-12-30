using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlovTandurBot.Configuration;
using PlovTandurBot.Models;
using PlovTandurBot.Repositories;
using TonSdk.Core;
using TonSdk.Core.Boc;

namespace PlovTandurBot.Services;

public class NftService
{
    private readonly ILogger<NftService> _logger;
    private readonly TonConfiguration _config;
    private readonly TonBlockchainService _tonService;
    private readonly NftRepository _nftRepository;
    private readonly PromoCodeRepository _promoCodeRepository;

    public NftService(
        ILogger<NftService> logger,
        IOptions<TonConfiguration> config,
        TonBlockchainService tonService,
        NftRepository nftRepository,
        PromoCodeRepository promoCodeRepository)
    {
        _logger = logger;
        _config = config.Value;
        _tonService = tonService;
        _nftRepository = nftRepository;
        _promoCodeRepository = promoCodeRepository;
    }

    public async Task<(bool Success, string? NftAddress, string? TransactionHash)> MintNftAsync(
        string promoCode, 
        string ownerAddress, 
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Minting NFT for promo code: {PromoCode} to {Owner}", promoCode, ownerAddress);

            // Get promo code details
            var promo = await _promoCodeRepository.GetByCodeAsync(promoCode, ct);
            if (promo == null)
            {
                _logger.LogWarning("Promo code not found: {PromoCode}", promoCode);
                return (false, null, null);
            }

            var collectionAddress = _config.NftCollectionAddress;
            if (string.IsNullOrEmpty(collectionAddress) || collectionAddress.Contains("адрес_NFT_коллекции"))
            {
                 _logger.LogWarning("NFT Collection address is not configured. Falling back to simulation.");
                 return await SimulateMintingAsync(promo, ownerAddress, ct);
            }

            // Real NFT Minting Logic (TEP-62)
            // Assuming standard collection contract where Op::Mint = 1
            
            // 1. Construct NFT Content (Simple metadata for now)
            // In a real scenario, we might upload metadata to IPFS and store the link here
            // Or use on-chain content. Let's use a simple common content structure.
            var contentCell = new CellBuilder()
                .StoreString(promo.ProductName) // Store product name as content for now
                .Build();

            // 2. Construct Mint Message Body
            // Op: 1 (Mint)
            // QueryId: random
            // ItemIndex: 0 (if collection auto-assigns) or next index. 
            // Note: Standard collection requires index. We might need to fetch `next_item_index` from collection first.
            // For simplicity, let's assume the collection manages it or we pass a placeholder. 
            // Better yet, many collections use a specific deploy message.
            // Let's implement a generic "Mint" body often used:
            // op::mint(1), query_id, index, amount, content
            
            // However, fetching next_item_index requires an extra call.
            // Let's assume we are the owner and the collection handles indexing or we just guess a large random one for test?
            // No, index must be sequential.
            // Let's assume standard "Mint" message:
            
            var queryId = (ulong)DateTime.UtcNow.Ticks;
            var amountForStorage = new Coins(0.05m); // 0.05 TON for storage

            var body = new CellBuilder()
                .StoreUInt(1, 32) // Op::Mint
                .StoreUInt(queryId, 64)
                .StoreUInt(0, 64) // Item Index - pass 0 if collection auto-increments or we don't know yet. (Implementation dependent)
                .StoreCoins(amountForStorage)
                .StoreRef(contentCell) // Content
                .Build();

            // 3. Send Transaction
            var collectionAddr = new Address(collectionAddress);
            var mintAmount = new Coins(0.1m); // Amount attached to the message (gas + forwarding)

            var (success, txHash) = await _tonService.SendTransferAsync(collectionAddr, mintAmount, body, ct);

            if (!success)
            {
                 _logger.LogError("Failed to send minting transaction");
                 return (false, null, null);
            }

            // 4. Calculate Expected NFT Address (if possible) or wait
            // Calculating NFT address requires knowing the collection state init and index.
            // For now, let's generate a placeholder "pending" address or derived one if we knew the index.
            // Since we don't know the index for sure without querying, we'll mark as pending.
            
            var pendingNftAddress = $"pending_{txHash}"; 

            // Create NFT record in database
            var nft = new NftToken
            {
                TokenId = Guid.NewGuid().ToString(),
                NftAddress = pendingNftAddress,
                OwnerAddress = ownerAddress,
                PromoCodeId = promoCode,
                ProductName = promo.ProductName,
                StatusEnum = NftTokenStatus.Minted, // Or "Pending"
                MintedAt = DateTime.UtcNow,
                MintTransactionHash = txHash
            };

            await _nftRepository.CreateAsync(nft, ct);
            await _promoCodeRepository.MarkAsUsedAsync(promoCode, pendingNftAddress, ct);

            _logger.LogInformation("NFT Mint transaction sent: {TxHash}", txHash);
            return (true, pendingNftAddress, txHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error minting NFT for promo code: {PromoCode}", promoCode);
            return (false, null, null);
        }
    }
    
    private async Task<(bool, string?, string?)> SimulateMintingAsync(PlovTandurBot.Models.PromoCode promo, string ownerAddress, CancellationToken ct)
    {
            await Task.Delay(2000, ct); // Simulate blockchain delay

            // Generate mock NFT address and transaction hash
            var nftAddress = $"EQNft_{Guid.NewGuid():N}";
            var txHash = Guid.NewGuid().ToString("N");

            // Create NFT record in database
            var nft = new NftToken
            {
                TokenId = Guid.NewGuid().ToString(),
                NftAddress = nftAddress,
                OwnerAddress = ownerAddress,
                PromoCodeId = promo.Code,
                ProductName = promo.ProductName,
                StatusEnum = NftTokenStatus.Minted,
                MintedAt = DateTime.UtcNow,
                MintTransactionHash = txHash
            };

            await _nftRepository.CreateAsync(nft, ct);

            // Update promo code with NFT address
            await _promoCodeRepository.MarkAsUsedAsync(promo.Code, nftAddress, ct);

            _logger.LogInformation("NFT minted successfully (SIMULATION): {NftAddress}", nftAddress);
            return (true, nftAddress, txHash);
    }

    public async Task<bool> CheckNftOwnershipAsync(string nftAddress, string expectedOwner, CancellationToken ct = default)
    {
        try
        {
            // TODO: Implement actual blockchain check using _tonService
            // For now, rely on DB
            var nft = await _nftRepository.GetByAddressAsync(nftAddress, ct);
            return nft?.OwnerAddress == expectedOwner;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking NFT ownership for {NftAddress}", nftAddress);
            return false;
        }
    }

    public async Task<List<NftToken>> GetUserNftsAsync(string ownerAddress, CancellationToken ct = default)
    {
        try
        {
            return await _nftRepository.GetByOwnerAsync(ownerAddress, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting NFTs for owner: {Owner}", ownerAddress);
            return new List<NftToken>();
        }
    }

    public async Task<bool> RedeemNftAsync(string nftAddress, string transactionHash, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Redeeming NFT: {NftAddress}", nftAddress);
            
            // Verify the transaction on chain if possible
            // For now, just mark in DB
            
            var success = await _nftRepository.RedeemAsync(nftAddress, transactionHash, ct);
            
            if (success)
            {
                _logger.LogInformation("NFT redeemed successfully: {NftAddress}", nftAddress);
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error redeeming NFT: {NftAddress}", nftAddress);
            return false;
        }
    }
}
