namespace ListenBridge.Core.Services;

public interface IListenSourceParser
{
    ListenParseResult ParseWithDiagnostics(string content, YouTubeListenFilterOptions? filterOptions = null);
}
