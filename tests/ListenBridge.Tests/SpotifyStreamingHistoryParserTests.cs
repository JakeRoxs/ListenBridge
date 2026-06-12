using ListenBridge.Spotify.Services;
using System.Text.Json;

namespace ListenBridge.Tests;

public class SpotifyStreamingHistoryParserTests
{
    [Fact]
    public void ParseWithDiagnostics_SimpleFixture_ParsesValidRowsAndReportsInvalidRows()
    {
        var parser = new SpotifyStreamingHistoryParser();

        var result = parser.ParseWithDiagnostics(ReadFixture("spotify-streaming-history-simple.json"));

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.FilteredInvalidDateRows);
        Assert.Single(result.Listens);
        Assert.Equal("Synthetic Artist", result.Listens[0].ArtistName);
        Assert.Equal("Synthetic Track", result.Listens[0].TrackName);
        Assert.Equal(new DateTimeOffset(2024, 1, 2, 13, 5, 0, TimeSpan.Zero), result.Listens[0].ListenedAt);
    }

    [Fact]
    public void ParseWithDiagnostics_ExtendedFixture_ParsesMetadataAndSpotifyUri()
    {
        var parser = new SpotifyStreamingHistoryParser();

        var result = parser.ParseWithDiagnostics(ReadFixture("spotify-streaming-history-extended.json"));

        Assert.Single(result.Listens);
        Assert.Equal("Extended Artist", result.Listens[0].ArtistName);
        Assert.Equal("Extended Track", result.Listens[0].TrackName);
        Assert.Equal(new DateTimeOffset(2024, 2, 3, 23, 59, 59, TimeSpan.Zero), result.Listens[0].ListenedAt);
        Assert.Equal("https://open.spotify.com/track/1234567890abcdef", result.Listens[0].OriginUrl?.ToString());
    }

    [Fact]
    public void ParseWithDiagnostics_ObjectRootThrowsJsonException()
    {
        var parser = new SpotifyStreamingHistoryParser();

        var exception = Assert.Throws<JsonException>(() => parser.ParseWithDiagnostics("{}"));

        Assert.Contains("must be a JSON array", exception.Message);
    }

    [Fact]
    public void ParseWithDiagnostics_NullContentThrowsArgumentNullException()
    {
        var parser = new SpotifyStreamingHistoryParser();

        Assert.Throws<ArgumentNullException>(() => parser.ParseWithDiagnostics(null!));
    }

    private static string ReadFixture(string fileName)
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        return File.ReadAllText(fixturePath);
    }
}
