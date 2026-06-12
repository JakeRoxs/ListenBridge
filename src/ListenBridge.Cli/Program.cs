using System.Text.Json;
using ListenBridge.Cli.Commands;
using ListenBridge.Cli.Reports;
using ListenBridge.ListenBrainz.Clients;
using ListenBridge.ListenBrainz.Services;
using ListenBridge.Core.Services;
using ListenBridge.Spotify.Services;
using ListenBridge.YouTube.Services;

namespace ListenBridge.Cli;

public static class Program
{
    private static async Task<int> Main(string[] args)
    {
        return await RunAsync(args, Console.Out, Console.Error).ConfigureAwait(false);
    }

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        if (output is null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        if (error is null)
        {
            throw new ArgumentNullException(nameof(error));
        }

        try
        {
            return await ExecuteAsync(args, output, error).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            await error.WriteLineAsync($"HTTP error: {exception.Message}").ConfigureAwait(false);
            return 1;
        }
        catch (JsonException exception)
        {
            await error.WriteLineAsync($"Invalid JSON: {exception.Message}").ConfigureAwait(false);
            return 1;
        }
        catch (UnauthorizedAccessException exception)
        {
            await error.WriteLineAsync($"File access error: {exception.Message}").ConfigureAwait(false);
            return 1;
        }
        catch (IOException exception)
        {
            await error.WriteLineAsync($"File I/O error: {exception.Message}").ConfigureAwait(false);
            return 1;
        }
        catch (OperationCanceledException)
        {
            await error.WriteLineAsync("Operation cancelled.").ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task<int> ExecuteAsync(string[] args, TextWriter output, TextWriter error)
    {
        var parseResult = CommandLineOptions.Parse(args, error);
        if (parseResult.Options is null)
        {
            return parseResult.ExitCode;
        }

        var options = parseResult.Options;

        if (!string.IsNullOrWhiteSpace(options.AuditExportPath))
        {
            var auditCommand = new AuditCommand(new ListenBrainzAuditService(options.AuditWindowSeconds));
            return await auditCommand.ExecuteAsync(options, output, error).ConfigureAwait(false);
        }

        var parser = CreateSourceParser(options.Source);
        var importCommand = new ImportCommand(
            parser,
            token => new ListenBrainzClient(token, options.ChunkSize),
            new ParsedListensJsonWriter(),
            new ParsedListensHtmlReportWriter());

        return await importCommand.ExecuteAsync(options, output, error).ConfigureAwait(false);
    }

    private static IListenSourceParser CreateSourceParser(ListenSource source)
    {
        return source switch
        {
            ListenSource.YouTube => new YouTubeTakeoutParser(),
            ListenSource.Spotify => new SpotifyStreamingHistoryParser(),
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unsupported listen source.")
        };
    }
}


