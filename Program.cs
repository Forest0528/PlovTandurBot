using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Google.Cloud.Firestore;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Serilog;
using PlovTandurBot.Configuration;
using PlovTandurBot.Services;
using PlovTandurBot.Repositories;

namespace PlovTandurBot;

class Program
{
    static async Task Main(string[] args)
    {
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File("logs/bot-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            Log.Information("Starting PlovTandur Bot System...");

            var host = Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureServices((context, services) =>
                {
                    var configuration = context.Configuration;

                    // Configure settings
                    services.Configure<BotConfiguration>(configuration.GetSection("BotConfiguration"));
                    services.Configure<FirebaseConfiguration>(configuration.GetSection("Firebase"));
                    services.Configure<TonConfiguration>(configuration.GetSection("Ton"));
                    services.Configure<VerifoneConfiguration>(configuration.GetSection("Verifone"));
                    services.Configure<NftMonitorConfiguration>(configuration.GetSection("NftMonitor"));

                    // Register Firebase
                    services.AddSingleton(sp =>
                    {
                        var config = sp.GetRequiredService<IOptions<FirebaseConfiguration>>().Value;
                        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", config.CredentialsPath);
                        return FirestoreDb.CreateAsync(config.ProjectId).GetAwaiter().GetResult();
                    });

                    // Register Telegram Bot Clients
                    services.AddSingleton<ITelegramBotClient>(sp =>
                    {
                        var config = sp.GetRequiredService<IOptions<BotConfiguration>>().Value;
                        return new TelegramBotClient(config.ClientBot.Token);
                    });

                    // Register second bot client for admin (named instance)
                    services.AddSingleton<TelegramBotClient>(sp =>
                    {
                        var config = sp.GetRequiredService<IOptions<BotConfiguration>>().Value;
                        return new TelegramBotClient(config.AdminBot.Token);
                    });

                    // Register Repositories
                    services.AddSingleton<UserRepository>();
                    services.AddSingleton<PromoCodeRepository>();
                    services.AddSingleton<NftRepository>();
                    services.AddSingleton<BroadcastRepository>();

                    // Register Services
                    services.AddSingleton<TonBlockchainService>();
                    services.AddSingleton<NftService>();
                    services.AddSingleton<ClientBotService>();
                    services.AddSingleton<AdminBotService>();
                    services.AddSingleton<BroadcastService>();

                    // Register Hosted Services
                    services.AddHostedService<BotHostedService>();
                })
                .Build();

            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}

// Hosted service to run both bots
public class BotHostedService : IHostedService
{
    private readonly ILogger<BotHostedService> _logger;
    private readonly ITelegramBotClient _clientBot;
    private readonly TelegramBotClient _adminBot;
    private readonly ClientBotService _clientBotService;
    private readonly AdminBotService _adminBotService;
    private CancellationTokenSource? _cts;

    public BotHostedService(
        ILogger<BotHostedService> logger,
        ITelegramBotClient clientBot,
        TelegramBotClient adminBot,
        ClientBotService clientBotService,
        AdminBotService adminBotService)
    {
        _logger = logger;
        _clientBot = clientBot;
        _adminBot = adminBot;
        _clientBotService = clientBotService;
        _adminBotService = adminBotService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = new CancellationTokenSource();

        _logger.LogInformation("Starting Telegram bots...");

        // Start client bot
        _clientBot.StartReceiving(
            updateHandler: async (bot, update, ct) => await _clientBotService.HandleUpdateAsync(update, ct),
            errorHandler: HandleErrorAsync,
            receiverOptions: new ReceiverOptions { AllowedUpdates = Array.Empty<Telegram.Bot.Types.Enums.UpdateType>() },
            cancellationToken: _cts.Token);

        var clientBotInfo = await _clientBot.GetMe(cancellationToken);
        _logger.LogInformation("Client bot started: @{Username}", clientBotInfo.Username);

        // Start admin bot
        _adminBot.StartReceiving(
            updateHandler: async (bot, update, ct) => await _adminBotService.HandleUpdateAsync(update, ct),
            errorHandler: HandleErrorAsync,
            receiverOptions: new ReceiverOptions { AllowedUpdates = Array.Empty<Telegram.Bot.Types.Enums.UpdateType>() },
            cancellationToken: _cts.Token);

        var adminBotInfo = await _adminBot.GetMe(cancellationToken);
        _logger.LogInformation("Admin bot started: @{Username}", adminBotInfo.Username);

        _logger.LogInformation("🤖 Both bots are running! Press Ctrl+C to stop.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Telegram bots...");
        _cts?.Cancel();
        return Task.CompletedTask;
    }

    private Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception, "Telegram bot error");
        return Task.CompletedTask;
    }
}