using Telegram.Bot.Types.ReplyMarkups;

namespace PlovTandurBot.Helpers;

public static class KeyboardBuilder
{
    public static InlineKeyboardMarkup CreateMainMenu(bool isVip = false)
    {
        var buttons = new List<InlineKeyboardButton[]>
        {
            new[] { InlineKeyboardButton.WithCallbackData("ℹ️ Помощь", "help") }
        };

        if (isVip)
        {
            buttons.Insert(0, new[] { InlineKeyboardButton.WithCallbackData("🎁 Мои NFT", "my_nfts") });
        }

        return new InlineKeyboardMarkup(buttons);
    }

    public static InlineKeyboardMarkup CreateWalletInstructions()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithUrl("📱 Telegram Wallet", "https://t.me/wallet"),
                InlineKeyboardButton.WithUrl("💎 TON Space", "https://tonkeeper.com/")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ У меня уже есть кошелек", "have_wallet")
            }
        });
    }

    public static InlineKeyboardMarkup CreateAdminMenu()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("➕ Создать промокод", "admin_create_promo") },
            new[] { InlineKeyboardButton.WithCallbackData("📊 Статистика", "admin_stats") },
            new[] { InlineKeyboardButton.WithCallbackData("📢 Рассылка", "admin_broadcast") },
            new[] { InlineKeyboardButton.WithCallbackData("📜 История", "admin_history") }
        });
    }

    public static InlineKeyboardMarkup CreateBroadcastMenu()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("👥 Всем", "broadcast_all") },
            new[] { InlineKeyboardButton.WithCallbackData("⭐ Только VIP", "broadcast_vip") },
            new[] { InlineKeyboardButton.WithCallbackData("👤 Обычным", "broadcast_regular") },
            new[] { InlineKeyboardButton.WithCallbackData("« Назад", "admin_menu") }
        });
    }

    public static InlineKeyboardMarkup CreateConfirmationKeyboard(string confirmCallback, string cancelCallback = "cancel")
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Подтвердить", confirmCallback),
                InlineKeyboardButton.WithCallbackData("❌ Отмена", cancelCallback)
            }
        });
    }
}
