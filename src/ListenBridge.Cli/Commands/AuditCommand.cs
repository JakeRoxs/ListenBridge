using System.Globalization;
using System.IO;
using System.Text;
using ListenBridge.ListenBrainz.Services;

namespace ListenBridge.Cli.Commands;

public sealed class AuditCommand
{
    private readonly ListenBrainzAuditService _auditService;

    public AuditCommand(ListenBrainzAuditService auditService)
    {
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    public async Task<int> ExecuteAsync(CommandLineOptions options, TextWriter output, TextWriter error)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (output is null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        if (error is null)
        {
            throw new ArgumentNullException(nameof(error));
        }

        if (string.IsNullOrWhiteSpace(options.AuditExportPath) || !File.Exists(options.AuditExportPath))
        {
            await error.WriteLineAsync($"The audit file at path '{options.AuditExportPath}' does not exist.").ConfigureAwait(false);
            return 1;
        }

        var json = await File.ReadAllTextAsync(options.AuditExportPath, Encoding.UTF8).ConfigureAwait(false);
        var result = _auditService.Audit(json);

        await output.WriteLineAsync("ListenBrainz Audit Results").ConfigureAwait(false);
        await output.WriteLineAsync($"  Total listens: {result.TotalListens}").ConfigureAwait(false);
        await output.WriteLineAsync($"  Parsed listens: {result.ParsedListens}").ConfigureAwait(false);
        await output.WriteLineAsync($"  Unique track identities: {result.UniqueIdentities}").ConfigureAwait(false);
        await output.WriteLineAsync($"  Exact duplicate entries: {result.ExactDuplicateCount}").ConfigureAwait(false);
        await output.WriteLineAsync($"  Near-duplicate entries (within {options.AuditWindowSeconds}s): {result.NearDuplicateCount}").ConfigureAwait(false);
        await output.WriteLineAsync($"  First listen: {FormatTimestamp(result.FirstListenedAt)}").ConfigureAwait(false);
        await output.WriteLineAsync($"  Last listen: {FormatTimestamp(result.LastListenedAt)}").ConfigureAwait(false);
        await output.WriteLineAsync("  Top identities:").ConfigureAwait(false);

        foreach (var item in result.TopItems)
        {
            await output.WriteLineAsync($"    {item.Identity}: {item.Count}").ConfigureAwait(false);
        }

        return 0;
    }

    private static string FormatTimestamp(long? unixTimeSeconds)
    {
        return unixTimeSeconds.HasValue
            ? DateTimeOffset.FromUnixTimeSeconds(unixTimeSeconds.Value).ToString("u", CultureInfo.InvariantCulture)
            : "n/a";
    }
}
