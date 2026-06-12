using System.Text.Json;
using ListenBridge.ListenBrainz.Services;
using Xunit;

namespace ListenBridge.Tests;

public class ListenBrainzAuditServiceTests
{
    [Fact]
    public void Audit_ReturnsSummary_ForValidExportJson()
    {
        var listens = new[]
        {
            new
            {
                listened_at = 1700000000L,
                track_metadata = new { artist_name = "Artist A", track_name = "Song A" }
            },
            new
            {
                listened_at = 1700000000L,
                track_metadata = new { artist_name = "Artist A", track_name = "Song A" }
            },
            new
            {
                listened_at = 1700000020L,
                track_metadata = new { artist_name = "Artist A", track_name = "Song A" }
            },
            new
            {
                listened_at = 1700000100L,
                track_metadata = new { artist_name = "Artist B", track_name = "Song B" }
            }
        };

        var json = JsonSerializer.Serialize(listens);
        var auditService = new ListenBrainzAuditService(30);
        var result = auditService.Audit(json);

        Assert.Equal(4, result.TotalListens);
        Assert.Equal(4, result.ParsedListens);
        Assert.Equal(2, result.UniqueIdentities);
        Assert.Equal(1, result.ExactDuplicateCount);
        Assert.Equal(2, result.NearDuplicateCount);
        Assert.Equal(1700000000L, result.FirstListenedAt);
        Assert.Equal(1700000100L, result.LastListenedAt);
        Assert.Contains(result.TopItems, item => item.Identity.Contains("Artist A|Song A") && item.Count == 3);
    }

    [Fact]
    public void Audit_IsCaseInsensitiveAboutArtistAndTrackIdentity()
    {
        var listens = new[]
        {
            new
            {
                listened_at = 1700000000L,
                track_metadata = new { artist_name = "Artist A", track_name = "Song A" }
            },
            new
            {
                listened_at = 1700000010L,
                track_metadata = new { artist_name = "artist a", track_name = "song a" }
            }
        };

        var json = JsonSerializer.Serialize(listens);
        var auditService = new ListenBrainzAuditService(30);
        var result = auditService.Audit(json);

        Assert.Equal(2, result.TotalListens);
        Assert.Equal(1, result.UniqueIdentities);
        Assert.Contains(result.TopItems, item => item.Identity == "Artist A|Song A" && item.Count == 2);
    }

    [Fact]
    public void Audit_ExcludesRowsWithoutTrackMetadataFromParsedTotals()
    {
        var rows = new object?[]
        {
            new
            {
                listened_at = 1700000000L,
                track_metadata = new { artist_name = "Artist A", track_name = "Song A" }
            },
            new
            {
                listened_at = 1700000010L,
                track_metadata = (object?)null
            },
            new
            {
                listened_at = 1700000020L
            }
        };

        var json = JsonSerializer.Serialize(rows);
        var auditService = new ListenBrainzAuditService(30);

        var result = auditService.Audit(json);

        Assert.Equal(3, result.TotalListens);
        Assert.Equal(1, result.ParsedListens);
        Assert.Equal(1, result.UniqueIdentities);
        Assert.Equal(1700000000L, result.FirstListenedAt);
        Assert.Equal(1700000000L, result.LastListenedAt);
        Assert.Single(result.TopItems);
    }

    [Fact]
    public void Audit_WithOnlyUnparseableRowsReturnsEmptyParsedSummary()
    {
        var json = "[{\"listened_at\":1700000000,\"track_metadata\":null}]";
        var auditService = new ListenBrainzAuditService(30);

        var result = auditService.Audit(json);

        Assert.Equal(1, result.TotalListens);
        Assert.Equal(0, result.ParsedListens);
        Assert.Equal(0, result.UniqueIdentities);
        Assert.Null(result.FirstListenedAt);
        Assert.Null(result.LastListenedAt);
        Assert.Empty(result.TopItems);
    }
}
