namespace PlovTandurBot.Configuration;

public class BotConfiguration
{
    public ClientBotSettings ClientBot { get; set; } = new();
    public AdminBotSettings AdminBot { get; set; } = new();
}

public class ClientBotSettings
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
}

public class AdminBotSettings
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public List<long> AllowedAdminIds { get; set; } = new();
}

public class FirebaseConfiguration
{
    public string ProjectId { get; set; } = string.Empty;
    public string CredentialsPath { get; set; } = string.Empty;
}

public class TonConfiguration
{
    public string Network { get; set; } = "testnet";
    public string Endpoint { get; set; } = string.Empty;
    public string BotMnemonic { get; set; } = string.Empty;
    public string CafeWalletAddress { get; set; } = string.Empty;
    public string NftCollectionAddress { get; set; } = string.Empty;
}

public class VerifoneConfiguration
{
    public string ApiKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public decimal MinimumOrderAmount { get; set; } = 200;
}

public class NftMonitorConfiguration
{
    public int CheckIntervalSeconds { get; set; } = 30;
    public bool Enabled { get; set; } = true;
}
