using System.Globalization;

namespace ListenBridge.YouTube.Services;

public static class YouTubeDateTimeParser
{
    private static readonly Dictionary<string, TimeSpan> TimeZoneOffsets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UTC"] = TimeSpan.Zero,
        ["GMT"] = TimeSpan.Zero,
        ["EST"] = TimeSpan.FromHours(-5),
        ["EDT"] = TimeSpan.FromHours(-4),
        ["CST"] = TimeSpan.FromHours(-6),
        ["CDT"] = TimeSpan.FromHours(-5),
        ["MST"] = TimeSpan.FromHours(-7),
        ["MDT"] = TimeSpan.FromHours(-6),
        ["PST"] = TimeSpan.FromHours(-8),
        ["PDT"] = TimeSpan.FromHours(-7),
    };

    private static readonly string[] SupportedFormats = new[]
    {
        "MMM d, yyyy, h:mm:ss tt z",
        "MMM d, yyyy, h:mm:ss tt K",
        "MMM d, yyyy, h:mm:ss tt' 'zzz",
        "MMM d, yyyy, h:mm:ss tt' 'z",
    };

    public static bool TryParse(string rawText, out DateTimeOffset listenedAt)
    {
        if (rawText is null)
        {
            listenedAt = default;
            return false;
        }

        var cleaned = rawText.Replace('\u00A0', ' ').Replace('\u202F', ' ').Trim();

        if (DateTimeOffset.TryParseExact(cleaned, SupportedFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out listenedAt))
        {
            return true;
        }

        var parts = cleaned.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 6)
        {
            return false;
        }

        var zonePart = parts[^1];
        if (!TimeZoneOffsets.TryGetValue(zonePart, out var offset))
        {
            if (!DateTimeOffset.TryParse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out listenedAt))
            {
                return false;
            }

            return true;
        }

        var localText = cleaned[..cleaned.LastIndexOf(zonePart, StringComparison.OrdinalIgnoreCase)].Trim();
        if (!DateTime.TryParseExact(localText, "MMM d, yyyy, h:mm:ss tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out var localDateTime))
        {
            listenedAt = default;
            return false;
        }

        listenedAt = new DateTimeOffset(localDateTime, offset).ToUniversalTime();
        return true;
    }
}
