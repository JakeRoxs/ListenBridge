namespace ListenBridge.Core.Services;

public sealed class YouTubeListenFilterOptions
{
    public int? SkipThresholdSeconds { get; init; }
    public int? DeduplicationWindowSeconds { get; init; }
    public bool IncludeTopicChannels { get; init; } = true;
}
