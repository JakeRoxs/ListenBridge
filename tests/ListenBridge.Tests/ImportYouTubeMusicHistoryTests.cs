using ListenBridge.Core.Domain;
using ListenBridge.Core.Services;
using Xunit;

namespace ListenBridge.Tests;

public class ImportYouTubeMusicHistoryTests
{
    [Fact]
    public async Task ImportAsync_FiltersByDateRangeAndSendsOnlyMatchingListens()
    {
        var parser = new FakeParser(new[]
        {
            new Listen("Artist A", "Track A", new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            new Listen("Artist B", "Track B", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero))
        });

        var client = new FakeClient();
        var importer = new ImportYouTubeMusicHistory(parser, client);

        var after = new DateTimeOffset(2023, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var before = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var result = await importer.ImportAsync("<html></html>", after, before);

        Assert.Equal(1, result.ListenCount);
        Assert.Single(client.Sent);
        Assert.Equal("Artist B", client.Sent[0].ArtistName);
    }

    [Fact]
    public void PrepareImportResult_SharedPipelineReturnsCleanedListensAndDiagnostics()
    {
        var parser = new FakeParser(new[]
        {
            new Listen("Artist A", "Track A", new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            new Listen("Artist B", "Track B", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero))
        });

        var after = new DateTimeOffset(2023, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var before = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var result = ImportYouTubeMusicHistory.PrepareImportResult(parser, "<html></html>", after, before);

        Assert.Equal(1, result.ListenCount);
        Assert.Equal(2, result.ParseResult.Listens.Count);
        Assert.Equal(1, result.DateFilteredRows);
        Assert.Equal("Artist B", result.Listens[0].ArtistName);
    }

    [Fact]
    public async Task ImportAsync_ReturnsParseDiagnosticsAndDateFilteredRows()
    {
        var parser = new FakeParser(new[]
        {
            new Listen("Artist A", "Track A", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), new Uri("https://www.youtube.com/watch?v=abc123")),
            new Listen("Artist A", "Track A", new DateTimeOffset(2024, 1, 1, 0, 0, 25, TimeSpan.Zero), new Uri("https://www.youtube.com/watch?v=abc123")),
            new Listen("Artist B", "Track B", new DateTimeOffset(2024, 1, 1, 0, 5, 0, TimeSpan.Zero), new Uri("https://www.youtube.com/watch?v=def456"))
        });

        var client = new FakeClient();
        var importer = new ImportYouTubeMusicHistory(parser, client);

        var after = new DateTimeOffset(2023, 12, 31, 0, 0, 0, TimeSpan.Zero);
        var before = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var options = new YouTubeListenFilterOptions { DeduplicationWindowSeconds = 30 };
        var result = await importer.ImportAsync("<html></html>", after, before, options);

        Assert.Equal(2, result.ListenCount);
        Assert.Equal(2, result.ParseResult.Listens.Count);
        Assert.Equal(0, result.DateFilteredRows);
        Assert.Equal(2, client.Sent.Count);
        Assert.Equal(3, result.ParseResult.TotalRows);
    }

    [Fact]
    public async Task ImportAsync_AppliesDeduplicationWindowBeforeSending()
    {
        var parser = new FakeParser(new[]
        {
            new Listen("Artist A", "Track A", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), new Uri("https://www.youtube.com/watch?v=abc123")),
            new Listen("Artist A", "Track A", new DateTimeOffset(2024, 1, 1, 0, 0, 25, TimeSpan.Zero), new Uri("https://www.youtube.com/watch?v=abc123")),
            new Listen("Artist B", "Track B", new DateTimeOffset(2024, 1, 1, 0, 5, 0, TimeSpan.Zero), new Uri("https://www.youtube.com/watch?v=def456"))
        });

        var client = new FakeClient();
        var importer = new ImportYouTubeMusicHistory(parser, client);

        var after = new DateTimeOffset(2023, 12, 31, 0, 0, 0, TimeSpan.Zero);
        var before = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var options = new YouTubeListenFilterOptions { DeduplicationWindowSeconds = 30 };
        var result = await importer.ImportAsync("<html></html>", after, before, options);

        Assert.Equal(2, result.ListenCount);
        Assert.Equal(2, client.Sent.Count);
        Assert.Contains(client.Sent, listen => listen.TrackName == "Track A");
        Assert.Contains(client.Sent, listen => listen.TrackName == "Track B");
    }

    private sealed class FakeParser : IYouTubeTakeoutParser
    {
        private readonly IReadOnlyList<Listen> _listens;

        public FakeParser(IReadOnlyList<Listen> listens)
        {
            _listens = listens;
        }

        public IReadOnlyList<Listen> Parse(string htmlContent) => _listens;

        public YouTubeParseResult ParseWithDiagnostics(string htmlContent, YouTubeListenFilterOptions? filterOptions = null)
        {
            var filtered = filterOptions is null
                ? _listens
                : YouTubeListenFilter.Apply(_listens, filterOptions);

            return new YouTubeParseResult
            {
                Listens = filtered,
                TotalRows = _listens.Count
            };
        }

        ListenParseResult IListenSourceParser.ParseWithDiagnostics(string content, YouTubeListenFilterOptions? filterOptions)
        {
            return ParseWithDiagnostics(content, filterOptions);
        }
    }

    private sealed class FakeClient : IListenBrainzClient
    {
        public List<Listen> Sent { get; } = new();

        public Task SendListensAsync(IEnumerable<Listen> listens, CancellationToken cancellationToken = default)
        {
            Sent.AddRange(listens);
            return Task.CompletedTask;
        }
    }
}
