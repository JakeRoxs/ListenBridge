namespace ListenBridge.Core.Domain;

public sealed class Listen
{
    public string ArtistName { get; init; }
    public string TrackName { get; init; }
    public DateTimeOffset ListenedAt { get; init; }
    public Uri? OriginUrl { get; init; }
    public bool IsTopicChannel { get; init; }

    public Listen(string artistName, string trackName, DateTimeOffset listenedAt, Uri? originUrl = null, bool isTopicChannel = false)
    {
        ArtistName = artistName ?? throw new ArgumentNullException(nameof(artistName));
        TrackName = trackName ?? throw new ArgumentNullException(nameof(trackName));
        ListenedAt = listenedAt;
        OriginUrl = originUrl;
        IsTopicChannel = isTopicChannel;
    }

    public ListenIdentity GetIdentity()
        => ListenIdentity.FromListen(this);
}
