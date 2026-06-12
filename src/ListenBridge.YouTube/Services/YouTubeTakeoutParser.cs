using System.Text.RegularExpressions;
using System.Net;
using HtmlAgilityPack;
using ListenBridge.Core.Domain;
using ListenBridge.Core.Services;

namespace ListenBridge.YouTube.Services;

public sealed class YouTubeTakeoutParser : IYouTubeTakeoutParser
{
    private static readonly Regex DateTimeTextRegex = new(
        @"[A-Za-z]{3} \d{1,2}, \d{4}, \d{1,2}:\d{2}:\d{2}\s*(?:AM|PM)(?:\s+[A-Za-z]{2,4})?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    public IReadOnlyList<Listen> Parse(string htmlContent)
    {
        return ParseWithDiagnostics(htmlContent, null).Listens;
    }

    public YouTubeParseResult ParseWithDiagnostics(string htmlContent, YouTubeListenFilterOptions? filterOptions = null)
    {
        if (htmlContent is null)
        {
            throw new ArgumentNullException(nameof(htmlContent));
        }

        var document = new HtmlDocument();
        document.LoadHtml(htmlContent);

        var rows = document.DocumentNode.SelectNodes(
            "//div[contains(@class,'outer-cell') and contains(@class,'mdl-cell--12-col') and contains(@class,'mdl-shadow--2dp')]"
        );

        if (rows is null)
        {
            return new YouTubeParseResult();
        }

        var parsedListens = new List<Listen>(rows.Count);
        var filteredNonMusicRows = 0;
        var filteredInvalidDateRows = 0;

        foreach (var row in rows)
        {
            if (TryParseListen(row, out var listen, out var failureReason))
            {
                parsedListens.Add(listen);
            }
            else if (failureReason == ParseFailureReason.NonMusicEntry)
            {
                filteredNonMusicRows++;
            }
            else
            {
                filteredInvalidDateRows++;
            }
        }

        var filteredTopicChannelsExcluded = 0;
        var filtered = parsedListens.AsEnumerable();

        if (filterOptions is not null && !filterOptions.IncludeTopicChannels)
        {
            var filteredList = parsedListens.Where(listen => !listen.IsTopicChannel).ToList();
            filteredTopicChannelsExcluded = parsedListens.Count(listen => listen.IsTopicChannel);
            filtered = filteredList;
        }

        var skipThresholdCount = 0;
        var dedupeCount = 0;
        var afterSkip = filtered;

        if (filterOptions?.SkipThresholdSeconds is int thresholdSeconds && thresholdSeconds > 0)
        {
            (afterSkip, skipThresholdCount) = CollapseRepeated(afterSkip, thresholdSeconds);
        }

        var afterDedupe = afterSkip;
        if (filterOptions?.DeduplicationWindowSeconds is int dedupeSeconds && dedupeSeconds > 0)
        {
            (afterDedupe, dedupeCount) = CollapseRepeated(afterSkip, dedupeSeconds);
        }

        return new YouTubeParseResult
        {
            Listens = afterDedupe.ToList(),
            TotalRows = rows.Count,
            FilteredNonMusicRows = filteredNonMusicRows,
            FilteredInvalidDateRows = filteredInvalidDateRows,
            FilteredTopicChannelsExcluded = filteredTopicChannelsExcluded,
            FilteredBySkipThreshold = skipThresholdCount,
            FilteredByDeduplication = dedupeCount
        };
    }

    private static (IReadOnlyList<Listen> Listens, int RemovedCount) CollapseRepeated(IEnumerable<Listen> listens, int windowSeconds)
    {
        var ordered = listens.OrderBy(listen => listen.ListenedAt).ToList();
        if (ordered.Count <= 1)
        {
            return (ordered, 0);
        }

        var deduplicated = new List<Listen>();
        var cluster = new List<Listen> { ordered[0] };
        var removed = 0;

        for (var index = 1; index < ordered.Count; index++)
        {
            var current = ordered[index];
            var lastInCluster = cluster[^1];

            if (lastInCluster.GetIdentity().Equals(current.GetIdentity()) && (current.ListenedAt - lastInCluster.ListenedAt).TotalSeconds <= windowSeconds)
            {
                cluster.Add(current);
                continue;
            }

            deduplicated.Add(cluster[^1]);
            removed += cluster.Count - 1;
            cluster = new List<Listen> { current };
        }

        deduplicated.Add(cluster[^1]);
        removed += cluster.Count - 1;
        return (deduplicated, removed);
    }

    private static bool TryParseListen(HtmlNode row, out Listen listen, out ParseFailureReason failureReason)
    {
        listen = null!;
        failureReason = ParseFailureReason.InvalidData;

        var titleNode = row.SelectSingleNode(".//p[contains(@class,'mdl-typography--title')]");
        var isYouTubeMusicSection = titleNode is not null && titleNode.InnerText.Contains("YouTube Music", StringComparison.OrdinalIgnoreCase);

        var topElement = GetTopElement(row);
        if (topElement is null)
        {
            failureReason = ParseFailureReason.InvalidData;
            return false;
        }

        var anchorNodes = topElement.SelectNodes(".//a");
        if (anchorNodes is null || anchorNodes.Count == 0)
        {
            failureReason = ParseFailureReason.InvalidData;
            return false;
        }

        var titleElement = anchorNodes[0];
        var channelElement = anchorNodes.Count >= 2 ? anchorNodes[1] : null;
        var isTopicChannel = channelElement is not null && channelElement.InnerText.Contains("- Topic", StringComparison.OrdinalIgnoreCase);

        if (!isYouTubeMusicSection && !isTopicChannel)
        {
            failureReason = ParseFailureReason.NonMusicEntry;
            return false;
        }

        var dateText = ExtractDateText(topElement.InnerText);
        if (dateText is null || !YouTubeDateTimeParser.TryParse(dateText, out var listenedAt))
        {
            failureReason = ParseFailureReason.InvalidData;
            return false;
        }

        var artistName = NormalizeArtistName(channelElement?.InnerText);
        var originUrl = GetSafeOriginUri(titleElement.GetAttributeValue("href", string.Empty));

        var trackName = DecodeText(titleElement.InnerText).Trim();
        if (string.IsNullOrWhiteSpace(trackName))
        {
            trackName = originUrl?.ToString() ?? string.Empty;
        }

        listen = new Listen(
            artistName: artistName,
            trackName: trackName,
            listenedAt: listenedAt,
            originUrl: originUrl,
            isTopicChannel: isTopicChannel);

        failureReason = ParseFailureReason.None;
        return true;
    }

    private enum ParseFailureReason
    {
        None,
        NonMusicEntry,
        InvalidData,
    }

    private static HtmlNode? GetTopElement(HtmlNode row)
    {
        var contentCells = row.SelectNodes(
            ".//div[contains(@class,'content-cell') and contains(@class,'mdl-cell--6-col') and contains(@class,'mdl-typography--body-1')]"
        );

        return contentCells switch
        {
            { Count: 2 } => contentCells[0],
            { Count: 6 } => contentCells[0].ParentNode,
            _ => null,
        };
    }

    private static string? ExtractDateText(string rawText)
    {
        var cleanedText = rawText.Replace('\u00A0', ' ').Replace('\u202F', ' ').Trim();
        var match = DateTimeTextRegex.Match(cleanedText);
        return match.Success ? match.Value.Trim() : null;
    }

    private static string NormalizeArtistName(string? rawArtistName)
    {
        var artistName = DecodeText(rawArtistName).Trim();
        if (!string.IsNullOrWhiteSpace(artistName) && artistName.EndsWith("- Topic", StringComparison.OrdinalIgnoreCase))
        {
            artistName = artistName[..^8].TrimEnd();
        }

        return artistName;
    }

    ListenParseResult IListenSourceParser.ParseWithDiagnostics(string content, YouTubeListenFilterOptions? filterOptions)
    {
        return ParseWithDiagnostics(content, filterOptions);
    }

    private static string DecodeText(string? value)
    {
        return WebUtility.HtmlDecode(value ?? string.Empty);
    }

    private static Uri? GetSafeOriginUri(string originHref)
    {
        if (Uri.TryCreate(originHref.Trim(), UriKind.Absolute, out var parsedUri) &&
            (string.Equals(parsedUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(parsedUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            return parsedUri;
        }

        return null;
    }
}
