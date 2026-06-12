using System.Text.Json.Serialization;

namespace ListenBridge.ListenBrainz.Models;

public sealed class ListenBrainzTrackMetadata
{
    [JsonPropertyName("artist_name")]
    public string ArtistName { get; init; } = string.Empty;

    [JsonPropertyName("track_name")]
    public string TrackName { get; init; } = string.Empty;

    [JsonPropertyName("additional_info")]
    public ListenBrainzAdditionalInfo? AdditionalInfo { get; init; }
}
