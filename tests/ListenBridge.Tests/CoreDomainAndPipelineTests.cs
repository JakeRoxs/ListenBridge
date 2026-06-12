using ListenBridge.Core.Domain;
using ListenBridge.Core.Services;

namespace ListenBridge.Tests;

public class CoreDomainAndPipelineTests
{
    [Fact]
    public void ListenIdentity_NormalizesValuesAndComparesCaseInsensitively()
    {
        var left = new ListenIdentity("  Artist Name  ", "  Track Name  ", "  HTTPS://EXAMPLE.COM/Track  ");
        var right = new ListenIdentity("artist name", "track name", "https://example.com/track");

        Assert.Equal("Artist Name", left.ArtistName);
        Assert.Equal("Track Name", left.TrackName);
        Assert.Equal("HTTPS://EXAMPLE.COM/Track", left.OriginUrl);
        Assert.Equal(left, right);
        Assert.True(left == right);
        Assert.False(left != right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.Equal("Artist Name|Track Name", left.ToAuditIdentity());
        Assert.Equal("Artist Name|Track Name|HTTPS://EXAMPLE.COM/Track", left.ToString());
    }

    [Fact]
    public void ListenIdentity_EmptyOriginIsOmittedFromString()
    {
        var identity = ListenIdentity.FromTrackMetadata(null, "  Track  ");

        Assert.Equal(string.Empty, identity.ArtistName);
        Assert.Equal("Track", identity.TrackName);
        Assert.Equal("|Track", identity.ToAuditIdentity());
        Assert.Equal("|Track", identity.ToString());
    }

    [Fact]
    public void ListenIdentity_FromListen_ThrowsOnNullListen()
    {
        Assert.Throws<ArgumentNullException>(() => ListenIdentity.FromListen(null!));
    }

    [Fact]
    public async Task ListenImportPipeline_ImportAsync_DoesNotSubmitWhenDateFilterRemovesAllListens()
    {
        var parser = new FakeListenSourceParser(new[]
        {
            new Listen("Artist", "Track", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero))
        });
        var destination = new RecordingScrobbleDestination();
        var pipeline = new ListenImportPipeline(parser, destination);

        var result = await pipeline.ImportAsync(
            "content",
            new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero),
            DateTimeOffset.MaxValue);

        Assert.Equal(0, result.ListenCount);
        Assert.Equal(1, result.DateFilteredRows);
        Assert.Equal(0, destination.SendCount);
    }

    [Fact]
    public async Task ListenImportPipeline_ImportAsync_SubmitsFilteredListensWhenPresent()
    {
        var expected = new Listen("Artist", "Track", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var parser = new FakeListenSourceParser(new[] { expected });
        var destination = new RecordingScrobbleDestination();
        var pipeline = new ListenImportPipeline(parser, destination);

        var result = await pipeline.ImportAsync(
            "content",
            DateTimeOffset.MinValue,
            DateTimeOffset.MaxValue);

        Assert.Equal(1, result.ListenCount);
        Assert.Equal(1, destination.SendCount);
        Assert.Same(expected, destination.SubmittedListens.Single());
    }

    private sealed class FakeListenSourceParser : IListenSourceParser
    {
        private readonly IReadOnlyList<Listen> _listens;

        public FakeListenSourceParser(IReadOnlyList<Listen> listens)
        {
            _listens = listens;
        }

        public ListenParseResult ParseWithDiagnostics(string content, YouTubeListenFilterOptions? filterOptions = null)
        {
            return new ListenParseResult
            {
                Listens = _listens,
                TotalRows = _listens.Count
            };
        }
    }

    private sealed class RecordingScrobbleDestination : IScrobbleDestination
    {
        public int SendCount { get; private set; }
        public IReadOnlyList<Listen> SubmittedListens { get; private set; } = Array.Empty<Listen>();

        public Task SendListensAsync(IEnumerable<Listen> listens, CancellationToken cancellationToken = default)
        {
            SendCount++;
            SubmittedListens = listens.ToList();
            return Task.CompletedTask;
        }
    }
}
