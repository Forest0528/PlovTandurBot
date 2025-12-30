using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using PlovTandurBot.Models;
using PlovTandurBot.Repositories;
using PlovTandurBot.Helpers;
using PlovTandurBot.Configuration;
using Microsoft.Extensions.Options;

namespace PlovTandurBot.Services;

public class AdminBotService
{
    private readonly ILogger<AdminBotService> _logger;
    private readonly ITelegramBotClient _botClient;
    private readonly AdminBotSettings _config;
    private readonly UserRepository _userRepository;
    private readonly PromoCodeRepository _promoCodeRepository;
    private readonly NftRepository _nftRepository;
    private readonly BroadcastService _broadcastService;
    private readonly Dictionary<long, string> _adminStates = new();

    public AdminBotService(
        ILogger<AdminBotService> logger,
        ITelegramBotClient botClient,
        IOptions<BotConfiguration> config,
        UserRepository userRepository,
        PromoCodeRepository promoCodeRepository,
        NftRepository nftRepository,
        BroadcastService broadcastService)
    {
        _logger = logger;
        _botClient = botClient;
        _config = config.Value.AdminBot;
        _userRepository = userRepository;
        _promoCodeRepository = promoCodeRepository;
        _nftRepository = nftRepository;
        _broadcastService = broadcastService;
    }

    public async Task HandleUpdateAsync(Update update, CancellationToken ct)
    {
        try
        {
            if (update.Message is { } message)
            {
                await HandleMessageAsync(message, ct);
            }
            else if (update.CallbackQuery is { } callbackQuery)
            {
                await HandleCallbackQueryAsync(callbackQuery, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling admin update");
        }
    }

    private bool IsAdmin(long userId)
    {
        return _config.AllowedAdminIds.Contains(userId);
    }

    private async Task HandleMessageAsync(Message message, CancellationToken ct)
    {
        if (message.Text is not { } messageText) return;

        var chatId = message.Chat.Id;

        if (!IsAdmin(chatId))
        {
            await _botClient.SendMessage(chatId, "❌ У вас нет доступа к этому боту.", cancellationToken: ct);
            return;
        }

        _logger.LogInformation("Admin {ChatId}: {Message}", chatId, messageText);

        if (messageText.StartsWith("/"))
        {
            await HandleCommandAsync(message, ct);
            return;
        }

        await HandleStateInputAsync(message, ct);
    }

    private async Task HandleCommandAsync(Message message, CancellationToken ct)
    {
        var command = message.Text!.Split(' ')[0].ToLower();

        switch (command)
        {
            case "/start":
            case "/menu":
                await ShowAdminMenuAsync(message.Chat.Id, ct);
                break;

            case "/stats":
                await ShowStatsAsync(message.Chat.Id, ct);
                break;

            default:
                await _botClient.SendMessage(
                    message.Chat.Id,
                    "Неизвестная команда. Используйте /menu для главного меню.",
                    cancellationToken: ct);
                break;
        }
    }

    private async Task ShowAdminMenuAsync(long chatId, CancellationToken ct)
    {
        _adminStates[chatId] = "Menu";

        await _botClient.SendMessage(
            chatId,
            MessageTemplates.Admin.Welcome,
            parseMode: ParseMode.Html,
            replyMarkup: KeyboardBuilder.CreateAdminMenu(),
            cancellationToken: ct);
    }

    private async Task ShowStatsAsync(long chatId, CancellationToken ct)
    {
        var allUsers = await _userRepository.GetAllAsync(ct);
        var vipUsers = allUsers.Count(u => u.UserTypeEnum == UserType.VIP);
        
        var allPromos = await _promoCodeRepository.GetByStatusAsync(PromoCodeStatus.New, ct);
        var activatedPromos = await _promoCodeRepository.GetByStatusAsync(PromoCodeStatus.Activated, ct);
        var usedPromos = await _promoCodeRepository.GetByStatusAsync(PromoCodeStatus.Used, ct);
        var totalPromos = allPromos.Count + activatedPromos.Count + usedPromos.Count;
        
        var allNfts = await _nftRepository.GetByStatusAsync(NftTokenStatus.Minted, ct);
        var activeNfts = await _nftRepository.GetByStatusAsync(NftTokenStatus.Active, ct);
        var redeemedNfts = await _nftRepository.GetByStatusAsync(NftTokenStatus.Redeemed, ct);
        var totalNfts = allNfts.Count + activeNfts.Count + redeemedNfts.Count;

        var message = string.Format(
            MessageTemplates.Admin.Stats,
            allUsers.Count,
            vipUsers,
            totalPromos,
            activatedPromos.Count + usedPromos.Count,
            totalNfts,
            redeemedNfts.Count);

        await _botClient.SendMessage(
            chatId,
            message,
            parseMode: ParseMode.Html,
            cancellationToken: ct);
    }

    private async Task HandleStateInputAsync(Message message, CancellationToken ct)
    {
        var chatId = message.Chat.Id;
        var input = message.Text!.Trim();

        if (!_adminStates.TryGetValue(chatId, out var state))
        {
            await ShowAdminMenuAsync(chatId, ct);
            return;
        }

        switch (state)
        {
            case "CreatingPromo":
                await HandlePromoCreationAsync(chatId, input, ct);
                break;

            case "BroadcastText":
                await HandleBroadcastTextAsync(chatId, input, ct);
                break;

            default:
                await ShowAdminMenuAsync(chatId, ct);
                break;
        }
    }

    private async Task HandlePromoCreationAsync(long chatId, string input, CancellationToken ct)
    {
        var parts = input.Split('|');
        if (parts.Length != 3)
        {
            await _botClient.SendMessage(
                chatId,
                "❌ Неверный формат. Используйте:\nКОД|Название|Описание",
                cancellationToken: ct);
            return;
        }

        var code = parts[0].Trim().ToUpper();
        var productName = parts[1].Trim();
        var description = parts[2].Trim();

        var promo = new PromoCode
        {
            Code = code,
            ProductName = productName,
            ProductDescription = description,
            StatusEnum = PromoCodeStatus.New,
            CreatedAt = DateTime.UtcNow
        };

        await _promoCodeRepository.CreateAsync(promo, ct);

        _adminStates[chatId] = "Menu";

        await _botClient.SendMessage(
            chatId,
            string.Format(MessageTemplates.Admin.PromoCodeCreated, code, productName),
            parseMode: ParseMode.Html,
            cancellationToken: ct);
    }

    private async Task HandleBroadcastTextAsync(long chatId, string text, CancellationToken ct)
    {
        // Get target audience from previous callback
        var audience = TargetAudience.All; // Default

        var broadcast = new BroadcastMessage
        {
            Text = text,
            TargetAudienceEnum = audience,
            CreatedByAdminId = chatId,
            StatusEnum = BroadcastStatus.Draft
        };

        // Send preview
        var users = await GetTargetUsersAsync(audience, ct);
        var preview = string.Format(
            MessageTemplates.Admin.BroadcastPreview,
            audience.ToString(),
            users.Count,
            text);

        await _botClient.SendMessage(
            chatId,
            preview,
            parseMode: ParseMode.Html,
            replyMarkup: KeyboardBuilder.CreateConfirmationKeyboard($"broadcast_send_{broadcast.MessageId}"),
            cancellationToken: ct);

        _adminStates[chatId] = "Menu";
    }

    private async Task<List<UserProfile>> GetTargetUsersAsync(TargetAudience audience, CancellationToken ct)
    {
        return audience switch
        {
            TargetAudience.VIP => await _userRepository.GetByTypeAsync(UserType.VIP, ct),
            TargetAudience.Regular => await _userRepository.GetByTypeAsync(UserType.Regular, ct),
            _ => await _userRepository.GetActiveUsersAsync(ct)
        };
    }

    private async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery, CancellationToken ct)
    {
        var chatId = callbackQuery.Message!.Chat.Id;
        var data = callbackQuery.Data!;

        await _botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: ct);

        switch (data)
        {
            case "admin_menu":
                await ShowAdminMenuAsync(chatId, ct);
                break;

            case "admin_create_promo":
                _adminStates[chatId] = "CreatingPromo";
                await _botClient.SendMessage(
                    chatId,
                    MessageTemplates.Admin.CreatePromoCode,
                    parseMode: ParseMode.Html,
                    cancellationToken: ct);
                break;

            case "admin_stats":
                await ShowStatsAsync(chatId, ct);
                break;

            case "admin_broadcast":
                await _botClient.SendMessage(
                    chatId,
                    MessageTemplates.Admin.BroadcastPrompt,
                    parseMode: ParseMode.Html,
                    replyMarkup: KeyboardBuilder.CreateBroadcastMenu(),
                    cancellationToken: ct);
                break;

            case "broadcast_all":
            case "broadcast_vip":
            case "broadcast_regular":
                _adminStates[chatId] = "BroadcastText";
                await _botClient.SendMessage(
                    chatId,
                    MessageTemplates.Admin.BroadcastTextPrompt,
                    cancellationToken: ct);
                break;

            default:
                if (data.StartsWith("broadcast_send_"))
                {
                    var messageId = data.Replace("broadcast_send_", "");
                    await _broadcastService.SendBroadcastAsync(messageId, chatId, ct);
                }
                break;
        }
    }
}
