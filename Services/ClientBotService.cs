using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using PlovTandurBot.Models;
using PlovTandurBot.Repositories;
using PlovTandurBot.Helpers;

namespace PlovTandurBot.Services;

public class ClientBotService
{
    private readonly ILogger<ClientBotService> _logger;
    private readonly ITelegramBotClient _botClient;
    private readonly UserRepository _userRepository;
    private readonly PromoCodeRepository _promoCodeRepository;
    private readonly NftService _nftService;
    private readonly TonBlockchainService _tonService;

    public ClientBotService(
        ILogger<ClientBotService> logger,
        ITelegramBotClient botClient,
        UserRepository userRepository,
        PromoCodeRepository promoCodeRepository,
        NftService nftService,
        TonBlockchainService tonService)
    {
        _logger = logger;
        _botClient = botClient;
        _userRepository = userRepository;
        _promoCodeRepository = promoCodeRepository;
        _nftService = nftService;
        _tonService = tonService;
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
            _logger.LogError(ex, "Error handling update");
        }
    }

    private async Task HandleMessageAsync(Message message, CancellationToken ct)
    {
        if (message.Text is not { } messageText) return;

        var chatId = message.Chat.Id;
        var username = message.From?.Username ?? "";

        _logger.LogInformation("User {ChatId} ({Username}): {Message}", chatId, username, messageText);

        // Get or create user
        var user = await _userRepository.GetOrCreateAsync(chatId, username, ct);

        // Handle commands
        if (messageText.StartsWith("/"))
        {
            await HandleCommandAsync(message, user, ct);
            return;
        }

        // Handle state-based input
        await HandleStateInputAsync(message, user, ct);
    }

    private async Task HandleCommandAsync(Message message, UserProfile user, CancellationToken ct)
    {
        var command = message.Text!.Split(' ')[0].ToLower();

        switch (command)
        {
            case "/start":
                await HandleStartCommandAsync(message.Chat.Id, user, ct);
                break;

            case "/help":
                await SendHelpMessageAsync(message.Chat.Id, ct);
                break;

            default:
                await _botClient.SendMessage(
                    message.Chat.Id,
                    "Неизвестная команда. Используйте /help для справки.",
                    cancellationToken: ct);
                break;
        }
    }

    private async Task HandleStartCommandAsync(long chatId, UserProfile user, CancellationToken ct)
    {
        user.State = "WaitingForPromo";
        await _userRepository.UpdateAsync(user, ct);

        await _botClient.SendMessage(
            chatId,
            MessageTemplates.Ru.Welcome,
            parseMode: ParseMode.Html,
            replyMarkup: KeyboardBuilder.CreateMainMenu(user.UserTypeEnum == UserType.VIP),
            cancellationToken: ct);

        await _botClient.SendMessage(
            chatId,
            MessageTemplates.Ru.EnterPromoCode,
            parseMode: ParseMode.Html,
            cancellationToken: ct);
    }

    private async Task SendHelpMessageAsync(long chatId, CancellationToken ct)
    {
        await _botClient.SendMessage(
            chatId,
            MessageTemplates.Ru.Help,
            parseMode: ParseMode.Html,
            cancellationToken: ct);
    }

    private async Task HandleStateInputAsync(Message message, UserProfile user, CancellationToken ct)
    {
        var chatId = message.Chat.Id;
        var input = message.Text!.Trim();

        switch (user.State)
        {
            case "WaitingForPromo":
                await HandlePromoCodeInputAsync(chatId, user, input, ct);
                break;

            case "WaitingForWallet":
                await HandleWalletInputAsync(chatId, user, input, ct);
                break;

            default:
                await HandleStartCommandAsync(chatId, user, ct);
                break;
        }
    }

    private async Task HandlePromoCodeInputAsync(long chatId, UserProfile user, string input, CancellationToken ct)
    {
        var code = ValidationHelper.NormalizePromoCode(input);

        if (!ValidationHelper.IsValidPromoCode(code))
        {
            await _botClient.SendMessage(
                chatId,
                MessageTemplates.Ru.InvalidPromoCode,
                parseMode: ParseMode.Html,
                cancellationToken: ct);
            return;
        }

        var promo = await _promoCodeRepository.GetByCodeAsync(code, ct);

        if (promo == null)
        {
            await _botClient.SendMessage(
                chatId,
                MessageTemplates.Ru.InvalidPromoCode,
                parseMode: ParseMode.Html,
                cancellationToken: ct);
            return;
        }

        if (promo.StatusEnum != PromoCodeStatus.New)
        {
            await _botClient.SendMessage(
                chatId,
                MessageTemplates.Ru.PromoCodeAlreadyUsed,
                parseMode: ParseMode.Html,
                cancellationToken: ct);
            return;
        }

        // Activate promo code
        await _promoCodeRepository.ActivateAsync(code, user.ChatId, ct);

        user.TempPromoCode = code;
        user.State = "WaitingForWallet";
        await _userRepository.UpdateAsync(user, ct);

        await _botClient.SendMessage(
            chatId,
            string.Format(MessageTemplates.Ru.PromoCodeAccepted, promo.ProductName),
            parseMode: ParseMode.Html,
            cancellationToken: ct);

        // Show wallet instructions
        await _botClient.SendMessage(
            chatId,
            MessageTemplates.Ru.NoWallet,
            replyMarkup: KeyboardBuilder.CreateWalletInstructions(),
            cancellationToken: ct);

        await _botClient.SendMessage(
            chatId,
            MessageTemplates.Ru.EnterWalletAddress,
            parseMode: ParseMode.Html,
            cancellationToken: ct);
    }

    private async Task HandleWalletInputAsync(long chatId, UserProfile user, string input, CancellationToken ct)
    {
        var address = ValidationHelper.NormalizeTonAddress(input);

        if (!ValidationHelper.IsValidTonAddress(address))
        {
            await _botClient.SendMessage(
                chatId,
                MessageTemplates.Ru.InvalidWalletAddress,
                parseMode: ParseMode.Html,
                cancellationToken: ct);
            return;
        }

        // Send minting message
        await _botClient.SendMessage(
            chatId,
            MessageTemplates.Ru.MintingNft,
            parseMode: ParseMode.Html,
            cancellationToken: ct);

        // Mint NFT
        var (success, nftAddress, txHash) = await _nftService.MintNftAsync(user.TempPromoCode, address, ct);

        if (!success || nftAddress == null)
        {
            await _botClient.SendMessage(
                chatId,
                MessageTemplates.Ru.MintingError,
                parseMode: ParseMode.Html,
                cancellationToken: ct);
            return;
        }

        // Update user
        user.WalletAddress = address;
        user.UserTypeEnum = UserType.VIP;
        user.State = "Start";
        user.TempPromoCode = "";
        await _userRepository.UpdateAsync(user, ct);

        // Send success message
        var explorerUrl = _tonService.GetNftExplorerUrl(nftAddress);
        await _botClient.SendMessage(
            chatId,
            string.Format(MessageTemplates.Ru.NftMinted, explorerUrl, _tonService.CafeWalletAddress),
            parseMode: ParseMode.Html,
            disableWebPagePreview: true,
            cancellationToken: ct);
    }

    private async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery, CancellationToken ct)
    {
        var chatId = callbackQuery.Message!.Chat.Id;
        var data = callbackQuery.Data;

        await _botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: ct);

        switch (data)
        {
            case "help":
                await SendHelpMessageAsync(chatId, ct);
                break;

            case "have_wallet":
                await _botClient.SendMessage(
                    chatId,
                    MessageTemplates.Ru.EnterWalletAddress,
                    parseMode: ParseMode.Html,
                    cancellationToken: ct);
                break;

            case "my_nfts":
                var user = await _userRepository.GetByIdAsync(chatId, ct);
                if (user != null && !string.IsNullOrEmpty(user.WalletAddress))
                {
                    var nfts = await _nftService.GetUserNftsAsync(user.WalletAddress, ct);
                    var message = nfts.Any()
                        ? $"💎 <b>Ваши NFT:</b>\n\n" + string.Join("\n", nfts.Select(n => $"• {n.ProductName} ({n.StatusEnum})"))
                        : "У вас пока нет NFT.";

                    await _botClient.SendMessage(chatId, message, parseMode: ParseMode.Html, cancellationToken: ct);
                }
                break;
        }
    }
}
