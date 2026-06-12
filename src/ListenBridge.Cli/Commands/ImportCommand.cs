using System.IO;
using System.Text;
using ListenBridge.Core.Services;
using ListenBridge.Core.Domain;
using ListenBridge.ListenBrainz.Clients;
using ListenBridge.Cli.Reports;

namespace ListenBridge.Cli.Commands;

public sealed class ImportCommand
{
    private readonly IListenSourceParser _parser;
    private readonly Func<string, IScrobbleDestination> _destinationFactory;
    private readonly ParsedListensJsonWriter _jsonWriter;
    private readonly ParsedListensHtmlReportWriter _htmlWriter;

    public ImportCommand(
        IListenSourceParser parser,
        Func<string, IScrobbleDestination> destinationFactory,
        ParsedListensJsonWriter jsonWriter,
        ParsedListensHtmlReportWriter htmlWriter)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _destinationFactory = destinationFactory ?? throw new ArgumentNullException(nameof(destinationFactory));
        _jsonWriter = jsonWriter ?? throw new ArgumentNullException(nameof(jsonWriter));
        _htmlWriter = htmlWriter ?? throw new ArgumentNullException(nameof(htmlWriter));
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

        if (!File.Exists(options.InputFile))
        {
            await error.WriteLineAsync($"The file at path '{options.InputFile}' does not exist.").ConfigureAwait(false);
            return 1;
        }

        var htmlContent = await File.ReadAllTextAsync(options.InputFile, Encoding.UTF8).ConfigureAwait(false);
        var dryRunResult = ListenImportPipeline.PrepareImportResult(_parser, htmlContent, options.After, options.Before, options.FilterOptions);

        if (options.DryRun)
        {
            var cleanedCount = dryRunResult.ParseResult.Listens.Count;
            await output.WriteLineAsync($"Parsed {dryRunResult.ParseResult.TotalRows} rows; {cleanedCount} listens after cleanup and before date filtering; {dryRunResult.ListenCount} listens after date filtering (dry run). ").ConfigureAwait(false);
            await output.WriteLineAsync($"  Filtered: {dryRunResult.ParseResult.FilteredRows} rows (non-music: {dryRunResult.ParseResult.FilteredNonMusicRows}, invalid: {dryRunResult.ParseResult.FilteredInvalidDateRows}, topic excluded: {dryRunResult.ParseResult.FilteredTopicChannelsExcluded}, skip-threshold removed: {dryRunResult.ParseResult.FilteredBySkipThreshold}, deduped: {dryRunResult.ParseResult.FilteredByDeduplication})").ConfigureAwait(false);

            if (dryRunResult.DateFilteredRows > 0)
            {
                await output.WriteLineAsync($"  Date filtered: {dryRunResult.DateFilteredRows} listens outside the selected range.").ConfigureAwait(false);
            }

            for (var index = 0; index < dryRunResult.ListenCount; index++)
            {
                var listen = dryRunResult.Listens[index];
                await output.WriteLineAsync($"{index}: {listen.TrackName} | {listen.ArtistName} | {listen.ListenedAt:O} | {listen.OriginUrl}").ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(options.Output.JsonPath))
            {
                await _jsonWriter.WriteJsonAsync(options.Output.JsonPath, dryRunResult.Listens).ConfigureAwait(false);
                await output.WriteLineAsync($"Wrote parsed output to {options.Output.JsonPath}").ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(options.Output.HtmlPath))
            {
                await _htmlWriter.WriteHtmlReportAsync(options.Output.HtmlPath, dryRunResult.Listens, options.InputFile).ConfigureAwait(false);
                await output.WriteLineAsync($"Wrote parsed HTML report to {options.Output.HtmlPath}").ConfigureAwait(false);
            }

            return 0;
        }

        if (string.IsNullOrWhiteSpace(options.UserToken))
        {
            await error.WriteLineAsync("--token is required unless --dry-run is specified.").ConfigureAwait(false);
            return 1;
        }

        var destination = _destinationFactory(options.UserToken);
        var importer = new ListenImportPipeline(_parser, destination);

        var result = await importer.ImportAsync(htmlContent, options.After, options.Before, options.FilterOptions).ConfigureAwait(false);
        await output.WriteLineAsync($"Submitted {result.ListenCount} listens.").ConfigureAwait(false);

        return 0;
    }
}
