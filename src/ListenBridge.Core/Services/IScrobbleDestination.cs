using ListenBridge.Core.Domain;

namespace ListenBridge.Core.Services;

public interface IScrobbleDestination
{
    Task SendListensAsync(IEnumerable<Listen> listens, CancellationToken cancellationToken = default);
}
