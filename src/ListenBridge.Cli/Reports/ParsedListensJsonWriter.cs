using System.Text;
using System.Text.Json;
using ListenBridge.Core.Domain;

namespace ListenBridge.Cli.Reports;

public sealed class ParsedListensJsonWriter
{
    public string BuildJson(IReadOnlyList<Listen> listens)
    {
        if (listens is null)
        {
            throw new ArgumentNullException(nameof(listens));
        }

        var outputData = listens.Select(listen => new
        {
            listen.ArtistName,
            listen.TrackName,
            listened_at = listen.ListenedAt.ToUnixTimeSeconds(),
            origin_url = listen.OriginUrl?.ToString()
        });

        return JsonSerializer.Serialize(outputData, new JsonSerializerOptions { WriteIndented = true });
    }

    public Task WriteJsonAsync(string outputPath, IReadOnlyList<Listen> listens)
    {
        if (outputPath is null)
        {
            throw new ArgumentNullException(nameof(outputPath));
        }

        ReportOutputDirectory.EnsureParentDirectoryExists(outputPath);
        return File.WriteAllTextAsync(outputPath, BuildJson(listens), Encoding.UTF8);
    }
}
