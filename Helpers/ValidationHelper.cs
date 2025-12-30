using System.Text.RegularExpressions;

namespace PlovTandurBot.Helpers;

public static class ValidationHelper
{
    private static readonly Regex TonAddressRegex = new(@"^[UEk0-9A-Za-z\-_]{48}$", RegexOptions.Compiled);
    private static readonly Regex PromoCodeRegex = new(@"^[A-Z0-9]{6,12}$", RegexOptions.Compiled);

    public static bool IsValidTonAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return false;

        return TonAddressRegex.IsMatch(address.Trim());
    }

    public static bool IsValidPromoCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        return PromoCodeRegex.IsMatch(code.Trim().ToUpper());
    }

    public static string NormalizeTonAddress(string address)
    {
        return address?.Trim() ?? string.Empty;
    }

    public static string NormalizePromoCode(string code)
    {
        return code?.Trim().ToUpper() ?? string.Empty;
    }
}
