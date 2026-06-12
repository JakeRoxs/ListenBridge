using System;
using System.Collections.Generic;
using ListenBridge.Core.Domain;
using ListenBridge.Core.Services;
using Xunit;

namespace ListenBridge.Tests;

public class YouTubeListenFilterTests
{
    [Fact]
    public void Apply_WhenExcludeTopicChannels_DropsTopicListens()
    {
        var listens = new[]
        {
            new Listen("Artist", "Track", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), new Uri("https://www.youtube.com/watch?v=abc"), isTopicChannel: true),
            new Listen("Artist", "Track", new DateTimeOffset(2024, 1, 1, 0, 2, 0, TimeSpan.Zero), new Uri("https://www.youtube.com/watch?v=def"), isTopicChannel: false),
        };

        var options = new YouTubeListenFilterOptions { IncludeTopicChannels = false };
        var result = YouTubeListenFilter.Apply(listens, options);

        Assert.Single(result);
        Assert.False(result[0].IsTopicChannel);
        Assert.Equal("https://www.youtube.com/watch?v=def", result[0].OriginUrl?.ToString());
    }

    [Fact]
    public void Apply_WhenDeduplicationUsesCaseInsensitiveIdentityComparison()
    {
        var listens = new List<Listen>
        {
            new Listen("Artist", "Track", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), new Uri("https://www.youtube.com/watch?v=ABC")),
            new Listen("artist", "track", new DateTimeOffset(2024, 1, 1, 0, 0, 25, TimeSpan.Zero), new Uri("https://www.youtube.com/watch?v=abc")),
        };

        var options = new YouTubeListenFilterOptions { DeduplicationWindowSeconds = 30 };
        var result = YouTubeListenFilter.Apply(listens, options);

        Assert.Single(result);
        Assert.Equal(new DateTimeOffset(2024, 1, 1, 0, 0, 25, TimeSpan.Zero), result[0].ListenedAt);
    }

    [Fact]
    public void Apply_WhenSkipThresholdSecondsRemovesRepeatedClusterEntries()
    {
        var listens = new List<Listen>
        {
            new Listen("Artist", "Track", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), new Uri("https://www.youtube.com/watch?v=abc")),
            new Listen("Artist", "Track", new DateTimeOffset(2024, 1, 1, 0, 0, 10, TimeSpan.Zero), new Uri("https://www.youtube.com/watch?v=abc")),
            new Listen("Artist", "Track", new DateTimeOffset(2024, 1, 1, 0, 0, 20, TimeSpan.Zero), new Uri("https://www.youtube.com/watch?v=abc")),
        };

        var options = new YouTubeListenFilterOptions { SkipThresholdSeconds = 30 };
        var result = YouTubeListenFilter.Apply(listens, options);

        Assert.Single(result);
        Assert.Equal(new DateTimeOffset(2024, 1, 1, 0, 0, 20, TimeSpan.Zero), result[0].ListenedAt);
    }

    [Fact]
    public void Apply_WhenDeduplicationUsesCaseInsensitiveOriginComparison()
    {
        var listens = new List<Listen>
        {
            new Listen("Artist", "Track", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), new Uri("https://www.youtube.com/watch?v=ABC")),
            new Listen("Artist", "Track", new DateTimeOffset(2024, 1, 1, 0, 0, 25, TimeSpan.Zero), new Uri("https://www.youtube.com/watch?v=abc")),
        };

        var options = new YouTubeListenFilterOptions { DeduplicationWindowSeconds = 30 };
        var result = YouTubeListenFilter.Apply(listens, options);

        Assert.Single(result);
        Assert.Equal(new DateTimeOffset(2024, 1, 1, 0, 0, 25, TimeSpan.Zero), result[0].ListenedAt);
    }

    [Fact]
    public void Apply_WhenDeduplicationWindowRemovesNearDuplicates()
    {
        var listens = new List<Listen>
        {
            new Listen("Artist", "Track", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), new Uri("https://www.youtube.com/watch?v=abc")),
            new Listen("Artist", "Track", new DateTimeOffset(2024, 1, 1, 0, 0, 20, TimeSpan.Zero), new Uri("https://www.youtube.com/watch?v=abc")),
            new Listen("Artist", "Track", new DateTimeOffset(2024, 1, 1, 0, 0, 50, TimeSpan.Zero), new Uri("https://www.youtube.com/watch?v=abc")),
            new Listen("Artist B", "Other", new DateTimeOffset(2024, 1, 1, 0, 5, 0, TimeSpan.Zero), new Uri("https://www.youtube.com/watch?v=def")),
        };

        var options = new YouTubeListenFilterOptions { DeduplicationWindowSeconds = 30 };
        var result = YouTubeListenFilter.Apply(listens, options);

        Assert.Equal(2, result.Count);
        Assert.Equal(new DateTimeOffset(2024, 1, 1, 0, 0, 50, TimeSpan.Zero), result[0].ListenedAt);
        Assert.Equal("Track", result[0].TrackName);
    }
}
