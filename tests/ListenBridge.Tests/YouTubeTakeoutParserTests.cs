using System.Globalization;
using ListenBridge.Core.Services;
using ListenBridge.YouTube.Services;
using Xunit;

namespace ListenBridge.Tests;

public class YouTubeTakeoutParserTests
{
    [Fact]
    public void Parse_WhenHtmlContainsValidWatchRow_ReturnsListen()
    {
        var html = @"
<div class=""outer-cell mdl-cell mdl-cell--12-col mdl-shadow--2dp"">
  <p class=""mdl-typography--title"">YouTube Music</p>
  <div class=""content-cell mdl-cell--6-col mdl-typography--body-1"">
    <a href=""https://www.youtube.com/watch?v=abc123"">Test Song</a> by <a href=""https://www.youtube.com/channel/chan"">Test Artist - Topic</a> Watched Apr 14, 2023, 4:20:00 PM UTC
  </div>
  <div class=""content-cell mdl-cell--6-col mdl-typography--body-1"">Other content</div>
</div>
";

        var parser = new YouTubeTakeoutParser();
        var listens = parser.Parse(html);

        Assert.Single(listens);
        var listen = listens[0];
        Assert.Equal("Test Artist", listen.ArtistName);
        Assert.Equal("Test Song", listen.TrackName);
        Assert.Equal(new DateTimeOffset(2023, 4, 14, 16, 20, 0, TimeSpan.Zero), listen.ListenedAt);
        Assert.Equal("https://www.youtube.com/watch?v=abc123", listen.OriginUrl?.ToString());
    }

    [Fact]
    public void Parse_WatchesWithNonBreakingSpaces_ParsesSuccessfully()
    {
        var html = "<div class=\"outer-cell mdl-cell mdl-cell--12-col mdl-shadow--2dp\">\n" +
                   "  <p class=\"mdl-typography--title\">YouTube Music</p>\n" +
                   "  <div class=\"content-cell mdl-cell mdl-cell--6-col mdl-typography--body-1\">\n" +
                   "    <a href=\"https://www.youtube.com/watch?v=abc123\">Another Song</a> by <a href=\"https://www.youtube.com/channel/chan\">Another Artist</a> Watched Apr 14, 2023, 4:20:00 PM\u00A0UTC\n" +
                   "  </div>\n" +
                   "  <div class=\"content-cell mdl-cell mdl-cell--6-col mdl-typography--body-1\">Other content</div>\n" +
                   "</div>\n";

        var parser = new YouTubeTakeoutParser();
        var listens = parser.Parse(html);

        Assert.Single(listens);
        Assert.Equal("Another Artist", listens[0].ArtistName);
        Assert.Equal("Another Song", listens[0].TrackName);
        Assert.Equal(new DateTimeOffset(2023, 4, 14, 16, 20, 0, TimeSpan.Zero), listens[0].ListenedAt);
    }

    [Fact]
    public void Parse_WhenRowHasOnlyOneAnchor_ReturnsListenWithMissingArtist()
    {
        var html = "<div class=\"outer-cell mdl-cell mdl-cell--12-col mdl-shadow--2dp\">\n" +
                   "  <p class=\"mdl-typography--title\">YouTube Music</p>\n" +
                   "  <div class=\"content-cell mdl-cell mdl-cell--6-col mdl-typography--body-1\">\n" +
                   "    Watched <a href=\"https://music.youtube.com/watch?v=abc123\">https://music.youtube.com/watch?v=abc123</a><br>Apr 14, 2023, 4:20:00 PM UTC\n" +
                   "  </div>\n" +
                   "  <div class=\"content-cell mdl-cell mdl-cell--6-col mdl-typography--body-1\">Other content</div>\n" +
                   "</div>\n";

        var parser = new YouTubeTakeoutParser();
        var listens = parser.Parse(html);

        Assert.Single(listens);
        Assert.Equal(string.Empty, listens[0].ArtistName);
        Assert.Equal("https://music.youtube.com/watch?v=abc123", listens[0].TrackName);
        Assert.Equal(new DateTimeOffset(2023, 4, 14, 16, 20, 0, TimeSpan.Zero), listens[0].ListenedAt);
    }

    [Fact]
    public void Parse_WhenRowHasTopicChannelWithoutYouTubeMusicTitle_ReturnsListen()
    {
        var html = @"
<div class=""outer-cell mdl-cell mdl-cell--12-col mdl-shadow--2dp"">
  <div class=""content-cell mdl-cell mdl-cell--6-col mdl-typography--body-1"">
    <a href=""https://www.youtube.com/watch?v=abc123"">Topic Song</a> by <a href=""https://www.youtube.com/channel/chan"">Test Artist - Topic</a> Watched Apr 14, 2023, 4:20:00 PM UTC
  </div>
  <div class=""content-cell mdl-cell mdl-cell--6-col mdl-typography--body-1"">Other content</div>
</div>
";

        var parser = new YouTubeTakeoutParser();
        var listens = parser.Parse(html);

        Assert.Single(listens);
        var listen = listens[0];
        Assert.Equal("Test Artist", listen.ArtistName);
        Assert.Equal("Topic Song", listen.TrackName);
        Assert.Equal(new DateTimeOffset(2023, 4, 14, 16, 20, 0, TimeSpan.Zero), listen.ListenedAt);
        Assert.True(listen.IsTopicChannel);
    }

    [Fact]
    public void ParseWithDiagnostics_ReportsFilteredRowsForNonMusicAndTopicExclusion()
    {
        var html = @"
<div class=""outer-cell mdl-cell mdl-cell--12-col mdl-shadow--2dp"">
  <p class=""mdl-typography--title"">YouTube Music</p>
  <div class=""content-cell mdl-cell mdl-cell--6-col mdl-typography--body-1"">
    <a href=""https://www.youtube.com/watch?v=abc123"">Song A</a> by <a href=""https://www.youtube.com/channel/chan"">Artist A</a> Watched Apr 14, 2023, 4:20:00 PM UTC
  </div>
  <div class=""content-cell mdl-cell mdl-cell--6-col mdl-typography--body-1"">Other content</div>
</div>
<div class=""outer-cell mdl-cell mdl-cell--12-col mdl-shadow--2dp"">
  <p class=""mdl-typography--title"">YouTube</p>
  <div class=""content-cell mdl-cell mdl-cell--6-col mdl-typography--body-1"">
    <a href=""https://www.youtube.com/watch?v=def456"">Other Video</a> by <a href=""https://www.youtube.com/channel/chan2"">Artist B</a> Watched Apr 14, 2023, 4:30:00 PM UTC
  </div>
  <div class=""content-cell mdl-cell mdl-cell--6-col mdl-typography--body-1"">Other content</div>
</div>
<div class=""outer-cell mdl-cell mdl-cell--12-col mdl-shadow--2dp"">
  <p class=""mdl-typography--title"">YouTube Music</p>
  <div class=""content-cell mdl-cell mdl-cell--6-col mdl-typography--body-1"">
    <a href=""https://www.youtube.com/watch?v=topic123"">Song B</a> by <a href=""https://www.youtube.com/channel/chanTopic"">Artist B - Topic</a> Watched Apr 14, 2023, 4:25:00 PM UTC
  </div>
  <div class=""content-cell mdl-cell mdl-cell--6-col mdl-typography--body-1"">Other content</div>
</div>
<div class=""outer-cell mdl-cell mdl-cell--12-col mdl-shadow--2dp"">
  <p class=""mdl-typography--title"">YouTube Music</p>
  <div class=""content-cell mdl-cell mdl-cell--6-col mdl-typography--body-1"">
    <a href=""https://www.youtube.com/watch?v=ghi789"">Song C</a> by <a href=""https://www.youtube.com/channel/chan3"">Artist C</a> Watched Invalid Date
  </div>
  <div class=""content-cell mdl-cell mdl-cell--6-col mdl-typography--body-1"">Other content</div>
</div>
";

        var parser = new YouTubeTakeoutParser();
        var result = parser.ParseWithDiagnostics(html, new YouTubeListenFilterOptions { IncludeTopicChannels = false });

        Assert.Equal(4, result.TotalRows);
        Assert.Equal(1, result.FilteredNonMusicRows);
        Assert.Equal(1, result.FilteredInvalidDateRows);
        Assert.Equal(1, result.FilteredTopicChannelsExcluded);
        Assert.Single(result.Listens);
    }

    [Fact]
    public void Parse_FixtureEdgeCases_ParsesSanitizedRowsSafely()
    {
        var parser = new YouTubeTakeoutParser();
        var listens = parser.Parse(ReadFixture("youtube-takeout-edge-cases.html"));

        Assert.Equal(4, listens.Count);

        Assert.Equal("Artist & Co.", listens[0].ArtistName);
        Assert.Equal("Track & Mix", listens[0].TrackName);
        Assert.Equal("https://music.youtube.com/watch?v=entity1", listens[0].OriginUrl?.ToString());
        Assert.Equal(new DateTimeOffset(2024, 1, 2, 1, 5, 0, TimeSpan.Zero), listens[0].ListenedAt);

        Assert.Equal(string.Empty, listens[1].ArtistName);
        Assert.Equal("https://music.youtube.com/watch?v=missing-artist", listens[1].TrackName);
        Assert.Equal("https://music.youtube.com/watch?v=missing-artist", listens[1].OriginUrl?.ToString());

        Assert.Equal("Relative Artist", listens[2].ArtistName);
        Assert.Equal("Relative Link Song", listens[2].TrackName);
        Assert.Null(listens[2].OriginUrl);

        Assert.Equal("Topic Artist", listens[3].ArtistName);
        Assert.Equal("Topic Channel Song", listens[3].TrackName);
        Assert.True(listens[3].IsTopicChannel);
    }

    [Fact]
    public void ParseWithDiagnostics_FixtureEdgeCases_CanExcludeTopicChannels()
    {
        var parser = new YouTubeTakeoutParser();
        var result = parser.ParseWithDiagnostics(
            ReadFixture("youtube-takeout-edge-cases.html"),
            new YouTubeListenFilterOptions { IncludeTopicChannels = false });

        Assert.Equal(4, result.TotalRows);
        Assert.Equal(1, result.FilteredTopicChannelsExcluded);
        Assert.Equal(3, result.Listens.Count);
        Assert.DoesNotContain(result.Listens, listen => listen.IsTopicChannel);
    }

    [Fact]
    public void Parse_FixtureAlternateDomShape_ReturnsListen()
    {
        var parser = new YouTubeTakeoutParser();
        var listens = parser.Parse(ReadFixture("youtube-takeout-alternate-dom.html"));

        Assert.Single(listens);
        Assert.Equal("Alternate Artist", listens[0].ArtistName);
        Assert.Equal("Alternate Shape Song", listens[0].TrackName);
        Assert.Equal(new DateTimeOffset(2024, 2, 3, 23, 59, 59, TimeSpan.Zero), listens[0].ListenedAt);
    }

    [Theory]
    [InlineData("Jan 2, 2024, 1:05:00 AM UTC", "2024-01-02T01:05:00+00:00")]
    [InlineData("Jan 2, 2024, 12:05:00 PM UTC", "2024-01-02T12:05:00+00:00")]
    [InlineData("Jan 2, 2024, 11:05:00 PM EST", "2024-01-03T04:05:00+00:00")]
    [InlineData("Jul 2, 2024, 11:05:00 PM PDT", "2024-07-03T06:05:00+00:00")]
    public void YouTubeDateTimeParser_TryParse_HandlesAmPmAndTimeZones(string rawText, string expected)
    {
        var parsed = YouTubeDateTimeParser.TryParse(rawText, out var listenedAt);

        Assert.True(parsed);
        Assert.Equal(DateTimeOffset.Parse(expected, CultureInfo.InvariantCulture), listenedAt);
    }

    private static string ReadFixture(string fileName)
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        return File.ReadAllText(fixturePath);
    }
}
