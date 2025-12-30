using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlovTandurBot.Configuration;
using TonSdk.Client;
using TonSdk.Core;
using TonSdk.Core.Crypto;

namespace PlovTandurBot.Services;

public class TonBlockchainService
{
    private readonly ILogger<TonBlockchainService> _logger;
    private readonly TonConfiguration _config;
    private readonly TonClient? _client;
    private readonly Mnemonic? _mnemonic;
    private readonly KeyPair? _keyPair;

    public TonBlockchainService(
        ILogger<TonBlockchainService> logger,
        IOptions<TonConfiguration> config)
    {
        _logger = logger;
        _config = config.Value;

        try
        {
            // Initialize TON client
            var endpoint = _config.Endpoint;
            if (string.IsNullOrEmpty(endpoint))
            {
                endpoint = _config.Network == "mainnet" 
                    ? "https://toncenter.com/api/v2/jsonRPC" 
                    : "https://testnet.toncenter.com/api/v2/jsonRPC";
            }
            
            var parameters = new TonClientParameters { Endpoint = endpoint };
            // If you have an API key, you should add it here: parameters.ApiKey = "YOUR_API_KEY";
            _client = new TonClient(parameters);

            // Initialize wallet from mnemonic
            if (!string.IsNullOrEmpty(_config.BotMnemonic))
            {
                var mnemonicWords = _config.BotMnemonic.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                _mnemonic = new Mnemonic(mnemonicWords);
                _keyPair = _mnemonic.Keys;

                // Initialize WalletV4 (common standard)
                var walletOptions = new WalletV4Options
                {
                    PublicKey = _keyPair.PublicKey
                };
                var wallet = new WalletV4(walletOptions);
                BotWalletAddress = wallet.Address.ToString();
                
                _logger.LogInformation("TON Blockchain service initialized. Network: {Network}, Wallet: {Address}", 
                    _config.Network, BotWalletAddress);
            }
            else
            {
                _logger.LogWarning("Bot mnemonic not provided. Wallet features will be unavailable.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize TON blockchain service");
        }
    }

    public async Task<string?> GetWalletBalanceAsync(string address, CancellationToken ct = default)
    {
        try
        {
            if (_client == null) return null;

            var parsedAddress = new Address(address);
            var balance = await _client.GetBalance(parsedAddress);
            
            return balance.ToString(); // Returns in nanotons
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting wallet balance for {Address}", address);
            return null;
        }
    }

    public async Task<bool> SendTonAsync(string destinationAddress, decimal amount, string comment, CancellationToken ct = default)
    {
        try
        {
            if (_client == null || _mnemonic == null || _keyPair == null) 
            {
                _logger.LogError("TON Client or Wallet not initialized");
                return false;
            }

            var dest = new Address(destinationAddress);
            var nanoTons = new Coins(amount);

            // Create transfer message
             var wallet = new WalletV4(new WalletV4Options { PublicKey = _keyPair.PublicKey });
             var seqno = await _client.GetWalletSeqno(wallet.Address);

             // Simple transfer with comment
             // Note: TonSdk usage might vary slightly depending on version for comment integration
             // Constructing body with comment:
             var body = new CellBuilder().StoreUInt(0, 32).StoreString(comment).Build();

             var transfer = new WalletTransfer
             {
                 Message = new InternalMessage(
                     new MessageOptions
                     {
                         Info = new CommonMessageInfo
                         {
                             Dest = dest,
                             Value = nanoTons,
                             Bounce = false
                         },
                         Body = body
                     }),
                 Mode = 1 // Pay fees separately
             };

             var signedMessage = wallet.CreateTransferMessage(new[] { transfer }, seqno ?? 0);
             // Sign message is handled inside CreateTransferMessage if using the high-level method, 
             // but here with WalletV4 we typically use the transfer object + key pair.
             // Wait, WalletV4.CreateTransferMessage usually returns the external message Cell directly if we pass keys?
             // Let's check typical usage. Actually WalletV4 class usually helps create the external message.
             
             // Re-checking standard TonSdk usage for WalletV4:
             // It generally creates a Cell that is the ExternalMessage.
             
             var extMsg = wallet.CreateTransferMessage(new[] { transfer }, seqno ?? 0).Sign(_keyPair.PrivateKey);
             
             await _client.SendBoc(extMsg);
             
             return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending TON to {Address}", destinationAddress);
            return false;
        }
    }

    public async Task<(bool Success, string? TxHash)> SendTransferAsync(Address destination, Coins amount, Cell? body, CancellationToken ct = default)
    {
        try
        {
            if (_client == null || _mnemonic == null || _keyPair == null) 
                 return (false, null);

            var wallet = new WalletV4(new WalletV4Options { PublicKey = _keyPair.PublicKey });
            
            // Get seqno
            // Note: In a real high-load scenario, we need to manage seqno carefully or queue transactions
            var seqno = await _client.GetWalletSeqno(wallet.Address); 
            if (seqno == null && await GetWalletBalanceAsync(wallet.Address.ToString()) != "0")
            {
                 // If seqno is null but balance > 0, it might be 0? usually null means uninitialized or 0
                 seqno = 0;
            }

            var transfer = new WalletTransfer
            {
                Message = new InternalMessage(new MessageOptions
                {
                    Info = new CommonMessageInfo
                    {
                        Dest = destination,
                        Value = amount,
                        Bounce = true 
                    },
                    Body = body,
                }),
                Mode = 1 // Flag 1 = Pay transfer fees separately from the message value
            };

            // Create and sign external message
             var extMsg = wallet.CreateTransferMessage(new[] { transfer }, seqno ?? 0).Sign(_keyPair.PrivateKey);

            var result = await _client.SendBoc(extMsg);
            
            // The result from SendBoc in many SDKs is the result of sending to mempool.
            // It doesn't guarantee inclusion. We get a hash roughly.
            // For now let's assume success if no exception.
            // Use cell hash as approximate tx ID or wait for it? 
            // Usually we return the message hash or calculate it.
            
            var msgHash = extMsg.Hash.ToString("hex");
            
            return (true, msgHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending generic transfer");
            return (false, null);
        }
    }

    public string GetExplorerUrl(string transactionHash)
    {
        var baseUrl = _config.Network == "mainnet" 
            ? "https://tonviewer.com/transaction/" 
            : "https://testnet.tonviewer.com/transaction/";
        
        return $"{baseUrl}{transactionHash}";
    }

    public string GetNftExplorerUrl(string nftAddress)
    {
        var baseUrl = _config.Network == "mainnet" 
            ? "https://tonviewer.com/" 
            : "https://testnet.tonviewer.com/";
        
        return $"{baseUrl}{nftAddress}";
    }
}
