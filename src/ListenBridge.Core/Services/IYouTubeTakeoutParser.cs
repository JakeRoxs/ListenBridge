using ListenBridge.Core.Domain;

namespace ListenBridge.Core.Services;

public interface IYouTubeTakeoutParser : IListenSourceParser
{
    IReadOnlyList<Listen> Parse(string htmlContent);
}
