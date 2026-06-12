using System.Text.Json.Serialization;

namespace ListenBridge.ListenBrainz.Models;

public sealed class ListenBrainzAdditionalInfo
{
    [JsonPropertyName("origin_url")]
    public string? OriginUrl { get; init; }

    [JsonPropertyName("music_service")]
    public string? MusicService { get; init; }

    [JsonPropertyName("media_player")]
    public string? MediaPlayer { get; init; }

    [JsonPropertyName("submission_client")]
    public string SubmissionClient { get; init; } = "ListenBridge";

    [JsonPropertyName("submission_client_version")]
    public string? SubmissionClientVersion { get; init; }
}
