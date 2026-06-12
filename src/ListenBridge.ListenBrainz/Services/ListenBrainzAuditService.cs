using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ListenBridge.Core.Domain;
using ListenBridge.ListenBrainz.Models;

namespace ListenBridge.ListenBrainz.Services;

public sealed class ListenBrainzAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly int _nearDuplicateWindowSeconds;

    public ListenBrainzAuditService(int nearDuplicateWindowSeconds = 60)
    {
        if (nearDuplicateWindowSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nearDuplicateWindowSeconds));
        }

        _nearDuplicateWindowSeconds = nearDuplicateWindowSeconds;
    }

    public ListenBrainzAuditResult Audit(string jsonExport)
    {
        if (jsonExport is null)
        {
            throw new ArgumentNullException(nameof(jsonExport));
        }

        var listens = JsonSerializer.Deserialize<ListenBrainzExportListen[]>(jsonExport, JsonOptions)
            ?? Array.Empty<ListenBrainzExportListen>();

        var totalListens = listens.Length;
        var ordered = listens
            .Where(l => l.TrackMetadata is not null)
            .OrderBy(l => l.ListenedAt)
            .ToList();

        var exactDuplicateCount = ordered
            .GroupBy(listen => (Identity: ToIdentity(listen).ToUpperInvariant(), listen.ListenedAt))
            .Sum(group => Math.Max(0, group.Count() - 1));

        var nearDuplicates = CountNearDuplicates(ordered);
        var uniqueTracks = ordered
            .Select(ToIdentity)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return new ListenBrainzAuditResult(
            totalListens,
            ordered.Count,
            uniqueTracks,
            exactDuplicateCount,
            nearDuplicates,
            ordered.FirstOrDefault()?.ListenedAt,
            ordered.LastOrDefault()?.ListenedAt,
            GetTopItems(ordered, 5)
        );
    }

    private int CountNearDuplicates(IReadOnlyList<ListenBrainzExportListen> ordered)
    {
        if (ordered.Count <= 1)
        {
            return 0;
        }

        var nearDuplicateCount = 0;
        for (var index = 1; index < ordered.Count; index++)
        {
            var previous = ordered[index - 1];
            var current = ordered[index];
            if (ToIdentity(previous).Equals(ToIdentity(current), StringComparison.OrdinalIgnoreCase) &&
                current.ListenedAt - previous.ListenedAt <= _nearDuplicateWindowSeconds)
            {
                nearDuplicateCount++;
            }
        }

        return nearDuplicateCount;
    }

    private static string ToIdentity(ListenBrainzExportListen listen)
    {
        var identity = ListenIdentity.FromTrackMetadata(
            listen.TrackMetadata?.ArtistName,
            listen.TrackMetadata?.TrackName);
        return identity.ToAuditIdentity();
    }

    private static IReadOnlyList<ListenBrainzAuditTopItem> GetTopItems(IReadOnlyList<ListenBrainzExportListen> ordered, int maxItems)
    {
        return ordered
            .GroupBy(ToIdentity, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ListenBrainzAuditTopItem(group.Key, group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Identity, StringComparer.OrdinalIgnoreCase)
            .Take(maxItems)
            .ToList();
    }
}

public sealed record ListenBrainzAuditResult(
    int TotalListens,
    int ParsedListens,
    int UniqueIdentities,
    int ExactDuplicateCount,
    int NearDuplicateCount,
    long? FirstListenedAt,
    long? LastListenedAt,
    IReadOnlyList<ListenBrainzAuditTopItem> TopItems);

public sealed record ListenBrainzAuditTopItem(string Identity, int Count);

internal sealed class ListenBrainzExportListen
{
    [JsonPropertyName("listened_at")]
    public long ListenedAt { get; init; }

    [JsonPropertyName("track_metadata")]
    public ListenBrainzTrackMetadata? TrackMetadata { get; init; }
}
