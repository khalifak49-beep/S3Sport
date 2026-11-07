using System.Text;

namespace S3Sport.Infrastructure;

/// <summary>Contact numbers typed by team managers: normalised for storage, and turned into call / WhatsApp links.</summary>
public static class PhoneHelper
{
    /// <summary>
    /// Keeps a leading "+" and the digits (Arabic-Indic digits become 0-9; spaces and dashes are dropped).
    /// Returns null unless 8 to 15 digits remain.
    /// </summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var s = input.Trim();
        var sb = new StringBuilder();
        if (s.StartsWith('+')) sb.Append('+');
        foreach (var c in s)
        {
            if (c is >= '0' and <= '9') sb.Append(c);
            else if (c is >= '٠' and <= '٩') sb.Append((char)('0' + (c - '٠')));
            else if (c is >= '۰' and <= '۹') sb.Append((char)('0' + (c - '۰')));
        }
        var digits = sb.ToString().TrimStart('+').Length;
        return digits is >= 8 and <= 15 ? sb.ToString() : null;
    }

    /// <summary>International digits for wa.me: an 8-digit local number is an Omani one (+968).</summary>
    public static string WhatsAppDigits(string phone)
    {
        var digits = new string(phone.Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith("00")) digits = digits[2..];
        return digits.Length == 8 ? "968" + digits : digits;
    }

    public static string WhatsAppUrl(string phone, string? text = null) =>
        $"https://wa.me/{WhatsAppDigits(phone)}" + (string.IsNullOrEmpty(text) ? "" : "?text=" + Uri.EscapeDataString(text));

    public static string TelUrl(string phone) => "tel:" + phone;
}
