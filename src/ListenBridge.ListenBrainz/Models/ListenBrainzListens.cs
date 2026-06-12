using System.Text.Json.Serialization;

namespace ListenBridge.ListenBrainz.Models;

public sealed class ListenBrainzListens
{
    [JsonPropertyName("listen_type")]
    public string ListenType { get; init; } = "import";

    [JsonPropertyName("payload")]
    public IReadOnlyList<ListenBrainzPayload> Payload { get; init; } = Array.Empty<ListenBrainzPayload>();
}
