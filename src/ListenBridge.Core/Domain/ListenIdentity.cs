namespace ListenBridge.Core.Domain;

public sealed class ListenIdentity : IEquatable<ListenIdentity>
{
    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    public ListenIdentity(string artistName, string trackName, string? originUrl = null)
    {
        ArtistName = Normalize(artistName);
        TrackName = Normalize(trackName);
        OriginUrl = Normalize(originUrl);
    }

    public string ArtistName { get; }
    public string TrackName { get; }
    public string OriginUrl { get; }

    public static ListenIdentity FromListen(Listen listen)
    {
        if (listen is null)
        {
            throw new ArgumentNullException(nameof(listen));
        }

        return new ListenIdentity(listen.ArtistName, listen.TrackName, listen.OriginUrl?.ToString());
    }

    public static ListenIdentity FromTrackMetadata(string? artistName, string? trackName)
        => new ListenIdentity(artistName ?? string.Empty, trackName ?? string.Empty);

    public static bool operator ==(ListenIdentity? left, ListenIdentity? right)
        => left is null ? right is null : left.Equals(right);

    public static bool operator !=(ListenIdentity? left, ListenIdentity? right)
        => !(left == right);

    public bool Equals(ListenIdentity? other)
    {
        if (other is null)
        {
            return false;
        }

        return Comparer.Equals(ArtistName, other.ArtistName)
            && Comparer.Equals(TrackName, other.TrackName)
            && Comparer.Equals(OriginUrl, other.OriginUrl);
    }

    public override bool Equals(object? obj)
        => Equals(obj as ListenIdentity);

    public override int GetHashCode()
        => HashCode.Combine(
            Comparer.GetHashCode(ArtistName),
            Comparer.GetHashCode(TrackName),
            Comparer.GetHashCode(OriginUrl));

    public string ToAuditIdentity()
        => $"{ArtistName}|{TrackName}";

    public override string ToString()
        => string.IsNullOrEmpty(OriginUrl)
            ? ToAuditIdentity()
            : $"{ArtistName}|{TrackName}|{OriginUrl}";

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
