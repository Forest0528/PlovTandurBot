using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using PlovTandurBot.Models;
using PlovTandurBot.Repositories;
using PlovTandurBot.Helpers;

namespace PlovTandurBot.Services;

public class BroadcastService
{
    private readonly ILogger<BroadcastService> _logger;
    private readonly ITelegramBotClient _clientBot;
    private readonly ITelegramBotClient _adminBot;
    private readonly UserRepository _userRepository;
    private readonly BroadcastRepository _broadcastRepository;

    public BroadcastService(
        ILogger<BroadcastService> logger,
        ITelegramBotClient clientBot,
        ITelegramBotClient adminBot,
        UserRepository userRepository,
        BroadcastRepository broadcastRepository)
    {
        _logger = logger;
        _clientBot = clientBot;
        _adminBot = adminBot;
        _userRepository = userRepository;
        _broadcastRepository = broadcastRepository;
    }

    public async Task<BroadcastMessage> CreateBroadcastAsync(
        string text,
        TargetAudience audience,
        long adminId,
        CancellationToken ct = default)
    {
        var broadcast = new BroadcastMessage
        {
            Text = text,
            TargetAudienceEnum = audience,
            CreatedByAdminId = adminId,
            StatusEnum = BroadcastStatus.Draft
        };

        await _broadcastRepository.CreateAsync(broadcast, ct);
        return broadcast;
    }

    public async Task SendBroadcastAsync(string messageId, long adminChatId, CancellationToken ct = default)
    {
        var broadcast = await _broadcastRepository.GetByIdAsync(messageId, ct);
        if (broadcast == null)
        {
            _logger.LogWarning("Broadcast not found: {MessageId}", messageId);
            return;
        }

        // Get target users
        var users = await GetTargetUsersAsync(broadcast.TargetAudienceEnum, ct);

        _logger.LogInformation("Starting broadcast {MessageId} to {Count} users", messageId, users.Count);

        // Update status
        broadcast.StatusEnum = BroadcastStatus.Sent;
        broadcast.SentAt = DateTime.UtcNow;
        await _broadcastRepository.UpdateAsync(broadcast, ct);

        // Send initial status to admin
        await _adminBot.SendMessage(
            adminChatId,
            string.Format(MessageTemplates.Admin.BroadcastStarted, 0, users.Count),
            parseMode: ParseMode.Html,
            cancellationToken: ct);

        int delivered = 0;
        int blocked = 0;
        int failed = 0;

        // Send to all users
        foreach (var user in users)
        {
            try
            {
                await _clientBot.SendMessage(
                    user.ChatId,
                    broadcast.Text,
                    parseMode: ParseMode.Html,
                    cancellationToken: ct);

                delivered++;

                // Update progress every 10 users
                if (delivered % 10 == 0)
                {
                    _logger.LogInformation("Broadcast progress: {Delivered}/{Total}", delivered, users.Count);
                }

                // Small delay to avoid rate limiting
                await Task.Delay(50, ct);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("blocked") || ex.Message.Contains("bot was blocked"))
                {
                    blocked++;
                    user.IsBlocked = true;
                    await _userRepository.UpdateAsync(user, ct);
                }
                else
                {
                    failed++;
                    _logger.LogWarning(ex, "Failed to send broadcast to user {ChatId}", user.ChatId);
                }
            }
        }

        // Update final stats
        broadcast.DeliveredCount = delivered;
        broadcast.BlockedCount = blocked;
        broadcast.FailedCount = failed;
        await _broadcastRepository.UpdateAsync(broadcast, ct);

        // Send completion message to admin
        await _adminBot.SendMessage(
            adminChatId,
            string.Format(MessageTemplates.Admin.BroadcastCompleted, delivered, blocked, failed),
            parseMode: ParseMode.Html,
            cancellationToken: ct);

        _logger.LogInformation(
            "Broadcast {MessageId} completed. Delivered: {Delivered}, Blocked: {Blocked}, Failed: {Failed}",
            messageId, delivered, blocked, failed);
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
}
