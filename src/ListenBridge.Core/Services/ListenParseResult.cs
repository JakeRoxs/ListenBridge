using ListenBridge.Core.Domain;

namespace ListenBridge.Core.Services;

public class ListenParseResult
{
    public IReadOnlyList<Listen> Listens { get; init; } = Array.Empty<Listen>();
    public int TotalRows { get; init; }
    public int FilteredNonMusicRows { get; init; }
    public int FilteredInvalidDateRows { get; init; }
    public int FilteredTopicChannelsExcluded { get; init; }
    public int FilteredBySkipThreshold { get; init; }
    public int FilteredByDeduplication { get; init; }
    public int FilteredRows => FilteredNonMusicRows + FilteredInvalidDateRows + FilteredTopicChannelsExcluded + FilteredBySkipThreshold + FilteredByDeduplication;
}
