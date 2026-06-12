using System.Globalization;
using System.Text.Json;
using ListenBridge.Core.Domain;
using ListenBridge.Core.Services;

namespace ListenBridge.Spotify.Services;

public sealed class SpotifyStreamingHistoryParser : IListenSourceParser
{
    public ListenParseResult ParseWithDiagnostics(string content, YouTubeListenFilterOptions? filterOptions = null)
    {
        if (content is null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        using var document = JsonDocument.Parse(content);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Spotify streaming history export must be a JSON array.");
        }

        var listens = new List<Listen>();
        var invalidRows = 0;
        var totalRows = 0;

        foreach (var row in document.RootElement.EnumerateArray())
        {
            totalRows++;
            if (TryParseListen(row, out var listen))
            {
                listens.Add(listen);
            }
            else
            {
                invalidRows++;
            }
        }

        return new ListenParseResult
        {
            Listens = listens,
            TotalRows = totalRows,
            FilteredInvalidDateRows = invalidRows
        };
    }

    private static bool TryParseListen(JsonElement row, out Listen listen)
    {
        listen = null!;

        if (!TryGetString(row, "artistName", "master_metadata_album_artist_name", out var artistName) ||
            !TryGetString(row, "trackName", "master_metadata_track_name", out var trackName) ||
            !TryGetString(row, "endTime", "ts", out var listenedAtText) ||
            !TryParseListenedAt(listenedAtText, out var listenedAt))
        {
            return false;
        }

        var originUrl = TryGetString(row, "spotify_track_uri", out var spotifyTrackUri)
            ? ToSpotifyTrackUri(spotifyTrackUri)
            : null;

        listen = new Listen(artistName, trackName, listenedAt, originUrl);
        return true;
    }

    private static bool TryGetString(JsonElement row, string propertyName, out string value)
    {
        if (row.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(property.GetString()))
        {
            value = property.GetString()!;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetString(JsonElement row, string simplePropertyName, string extendedPropertyName, out string value)
    {
        return TryGetString(row, simplePropertyName, out value) || TryGetString(row, extendedPropertyName, out value);
    }

    private static bool TryParseListenedAt(string value, out DateTimeOffset listenedAt)
    {
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out listenedAt))
        {
            return true;
        }

        if (DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dateTime))
        {
            listenedAt = new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc));
            return true;
        }

        listenedAt = default;
        return false;
    }

    private static Uri? ToSpotifyTrackUri(string spotifyTrackUri)
    {
        const string Prefix = "spotify:track:";
        if (!spotifyTrackUri.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var trackId = spotifyTrackUri[Prefix.Length..];
        return Uri.TryCreate($"https://open.spotify.com/track/{trackId}", UriKind.Absolute, out var uri) ? uri : null;
    }
}
