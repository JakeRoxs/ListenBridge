using System.Text.Json.Serialization;
using ListenBridge.Core.Domain;

namespace ListenBridge.ListenBrainz.Models;

public sealed class ListenBrainzPayload
{
    [JsonPropertyName("listened_at")]
    public long ListenedAt { get; init; }

    [JsonPropertyName("track_metadata")]
    public required ListenBrainzTrackMetadata TrackMetadata { get; init; }

    public static ListenBrainzPayload FromDomain(Listen listen)
    {
        if (listen is null)
        {
            throw new ArgumentNullException(nameof(listen));
        }

        return new ListenBrainzPayload
        {
            ListenedAt = listen.ListenedAt.ToUnixTimeSeconds(),
            TrackMetadata = new ListenBrainzTrackMetadata
            {
                ArtistName = listen.ArtistName,
                TrackName = listen.TrackName,
                AdditionalInfo = CreateAdditionalInfo(listen)
            }
        };
    }

    private static ListenBrainzAdditionalInfo CreateAdditionalInfo(Listen listen)
    {
        var originUrl = listen.OriginUrl?.ToString();
        return new ListenBrainzAdditionalInfo
        {
            OriginUrl = originUrl,
            MusicService = DeriveMusicService(listen.OriginUrl),
            MediaPlayer = "YouTube Music",
            SubmissionClientVersion = typeof(ListenBrainzPayload).Assembly.GetName().Version?.ToString()
        };
    }

    private static string? DeriveMusicService(Uri? originUrl)
    {
        if (originUrl is null)
        {
            return null;
        }

        var host = originUrl.Host.Trim();
        return string.IsNullOrWhiteSpace(host) ? null : host;
    }
}
