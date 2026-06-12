using System.Globalization;
using System.IO;
using ListenBridge.Core.Services;

namespace ListenBridge.Cli;

public sealed record CommandLineOptions(
    string InputFile,
    ListenSource Source,
    string UserToken,
    int ChunkSize,
    DateTimeOffset After,
    DateTimeOffset Before,
    bool DryRun,
    OutputOptions Output,
    YouTubeListenFilterOptions FilterOptions,
    string? AuditExportPath,
    int AuditWindowSeconds)
{
    public static CommandLineParseResult Parse(string[] args, TextWriter error)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        if (error is null)
        {
            throw new ArgumentNullException(nameof(error));
        }

        if (args.Length == 0)
        {
            PrintHelp(error);
            return CommandLineParseResult.Failure(1);
        }

        var state = new ParseState();

        for (var index = 0; index < args.Length; index++)
        {
            var result = ParseOption(args, ref index, error, state);
            if (result is OptionParseResult.Failure)
            {
                return CommandLineParseResult.Failure(1);
            }

            if (result is OptionParseResult.Help)
            {
                return CommandLineParseResult.Failure(0);
            }
        }

        if (!ValidateRequiredOptions(state, error))
        {
            return CommandLineParseResult.Failure(1);
        }

        return CommandLineParseResult.Success(state.ToOptions());
    }

    private static OptionParseResult ParseOption(string[] args, ref int index, TextWriter error, ParseState state)
    {
        var arg = args[index];
        switch (arg)
        {
            case "-i":
            case "--input":
                return TrySetStringOption(args, ref index, arg, error, value => state.InputFile = value);
            case "-t":
            case "--token":
                return TrySetStringOption(args, ref index, arg, error, value => state.UserToken = value);
            case "--source":
                return TrySetSource(args, ref index, arg, error, state);
            case "--chunk-size":
                return TrySetPositiveInt(args, ref index, arg, error, value => state.ChunkSize = value);
            case "--after":
                return TrySetUnixTime(args, ref index, arg, error, value => state.After = value);
            case "--before":
                return TrySetUnixTime(args, ref index, arg, error, value => state.Before = value);
            case "--dry-run":
                state.DryRun = true;
                return OptionParseResult.Parsed;
            case "--skip-threshold-seconds":
                return TrySetNonNegativeInt(args, ref index, arg, error, value => state.SkipThresholdSeconds = value);
            case "--dedupe-window-seconds":
                return TrySetNonNegativeInt(args, ref index, arg, error, value => state.DeduplicationWindowSeconds = value);
            case "--audit-export":
                return TrySetStringOption(args, ref index, arg, error, value => state.AuditExportPath = value);
            case "--audit-window-seconds":
                return TrySetPositiveInt(args, ref index, arg, error, value => state.AuditWindowSeconds = value);
            case "--exclude-topic-channels":
                state.IncludeTopicChannels = false;
                return OptionParseResult.Parsed;
            case "--output-json":
                return TrySetStringOption(args, ref index, arg, error, value => state.OutputJsonPath = value);
            case "--output-html":
                return TrySetStringOption(args, ref index, arg, error, value => state.OutputHtmlPath = value);
            case "-h":
            case "--help":
                PrintHelp(error);
                return OptionParseResult.Help;
            default:
                error.WriteLine($"Unknown argument: {arg}");
                PrintHelp(error);
                return OptionParseResult.Failure;
        }
    }

    private static bool ValidateRequiredOptions(ParseState state, TextWriter error)
    {
        if (string.IsNullOrWhiteSpace(state.AuditExportPath) && string.IsNullOrWhiteSpace(state.InputFile))
        {
            error.WriteLine("--input is required unless --audit-export is specified.");
            PrintHelp(error);
            return false;
        }

        if (!state.DryRun && string.IsNullOrWhiteSpace(state.UserToken) && string.IsNullOrWhiteSpace(state.AuditExportPath))
        {
            error.WriteLine("--token is required for submission.");
            PrintHelp(error);
            return false;
        }

        return true;
    }

    private static OptionParseResult TrySetStringOption(string[] args, ref int index, string optionName, TextWriter error, Action<string> setValue)
    {
        if (!TryGetNextValue(args, ref index, optionName, error, out var value))
        {
            return OptionParseResult.Failure;
        }

        setValue(value);
        return OptionParseResult.Parsed;
    }

    private static OptionParseResult TrySetPositiveInt(string[] args, ref int index, string optionName, TextWriter error, Action<int> setValue)
    {
        if (!TryGetNextValue(args, ref index, optionName, error, out var rawValue) ||
            !TryParsePositiveInt(rawValue, optionName, error, out var value))
        {
            return OptionParseResult.Failure;
        }

        setValue(value);
        return OptionParseResult.Parsed;
    }

    private static OptionParseResult TrySetNonNegativeInt(string[] args, ref int index, string optionName, TextWriter error, Action<int> setValue)
    {
        if (!TryGetNextValue(args, ref index, optionName, error, out var rawValue) ||
            !TryParseNonNegativeInt(rawValue, optionName, error, out var value))
        {
            return OptionParseResult.Failure;
        }

        setValue(value);
        return OptionParseResult.Parsed;
    }

    private static OptionParseResult TrySetUnixTime(string[] args, ref int index, string optionName, TextWriter error, Action<DateTimeOffset> setValue)
    {
        if (!TryGetNextValue(args, ref index, optionName, error, out var rawValue) ||
            !TryParseLong(rawValue, optionName, error, out var seconds) ||
            !TryConvertUnixTime(seconds, optionName, error, out var value))
        {
            return OptionParseResult.Failure;
        }

        setValue(value);
        return OptionParseResult.Parsed;
    }

    private static OptionParseResult TrySetSource(string[] args, ref int index, string optionName, TextWriter error, ParseState state)
    {
        if (!TryGetNextValue(args, ref index, optionName, error, out var rawValue) ||
            !TryParseSource(rawValue, error, out var source))
        {
            return OptionParseResult.Failure;
        }

        state.Source = source;
        return OptionParseResult.Parsed;
    }

    private sealed class ParseState
    {
        public string? InputFile { get; set; }
        public ListenSource Source { get; set; } = ListenSource.YouTube;
        public string UserToken { get; set; } = string.Empty;
        public int ChunkSize { get; set; } = 200;
        public DateTimeOffset After { get; set; } = DateTimeOffset.MinValue;
        public DateTimeOffset Before { get; set; } = DateTimeOffset.MaxValue;
        public bool DryRun { get; set; }
        public int SkipThresholdSeconds { get; set; }
        public int DeduplicationWindowSeconds { get; set; }
        public int AuditWindowSeconds { get; set; } = 60;
        public bool IncludeTopicChannels { get; set; } = true;
        public string? OutputJsonPath { get; set; }
        public string? OutputHtmlPath { get; set; }
        public string? AuditExportPath { get; set; }

        public CommandLineOptions ToOptions()
        {
            var filterOptions = new YouTubeListenFilterOptions
            {
                SkipThresholdSeconds = SkipThresholdSeconds > 0 ? SkipThresholdSeconds : null,
                DeduplicationWindowSeconds = DeduplicationWindowSeconds > 0 ? DeduplicationWindowSeconds : null,
                IncludeTopicChannels = IncludeTopicChannels,
            };

            return new CommandLineOptions(
            InputFile ?? string.Empty,
            Source,
            UserToken,
            ChunkSize,
            After,
            Before,
            DryRun,
            new OutputOptions(OutputJsonPath, OutputHtmlPath),
            filterOptions,
            AuditExportPath,
            AuditWindowSeconds);
        }
    }

    private enum OptionParseResult
    {
        Parsed,
        Help,
        Failure,
    }

    private static bool TryGetNextValue(string[] args, ref int index, string optionName, TextWriter error, out string value)
    {
        if (index + 1 >= args.Length)
        {
            error.WriteLine($"Missing value for {optionName}");
            value = string.Empty;
            return false;
        }

        index++;
        value = args[index];
        return true;
    }

    private static bool TryParseInt(string rawValue, string optionName, TextWriter error, out int value)
    {
        if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            error.WriteLine($"Invalid integer value for {optionName}: {rawValue}");
            return false;
        }

        return true;
    }

    private static bool TryParsePositiveInt(string rawValue, string optionName, TextWriter error, out int value)
    {
        if (!TryParseInt(rawValue, optionName, error, out value))
        {
            return false;
        }

        if (value <= 0)
        {
            error.WriteLine($"Invalid value for {optionName}: {rawValue}. Value must be greater than 0.");
            return false;
        }

        return true;
    }

    private static bool TryParseNonNegativeInt(string rawValue, string optionName, TextWriter error, out int value)
    {
        if (!TryParseInt(rawValue, optionName, error, out value))
        {
            return false;
        }

        if (value < 0)
        {
            error.WriteLine($"Invalid value for {optionName}: {rawValue}. Value must be 0 or greater.");
            return false;
        }

        return true;
    }

    private static bool TryParseLong(string rawValue, string optionName, TextWriter error, out long value)
    {
        if (!long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            error.WriteLine($"Invalid integer value for {optionName}: {rawValue}");
            return false;
        }

        return true;
    }

    private static bool TryParseSource(string rawValue, TextWriter error, out ListenSource source)
    {
        if (Enum.TryParse(rawValue, ignoreCase: true, out source) && Enum.IsDefined(source))
        {
            return true;
        }

        error.WriteLine($"Invalid source: {rawValue}. Supported sources: youtube, spotify.");
        source = default;
        return false;
    }

    private static bool TryConvertUnixTime(long seconds, string optionName, TextWriter error, out DateTimeOffset dateTimeOffset)
    {
        try
        {
            dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(seconds);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            error.WriteLine($"Invalid unix timestamp for {optionName}: {seconds}");
            dateTimeOffset = default;
            return false;
        }
    }

    private static void PrintHelp(TextWriter error)
    {
        error.WriteLine("Usage: ListenBridge.Cli --input <path> [--token <listenbrainz-token>] [options]");
        error.WriteLine();
        error.WriteLine("Options:");
        error.WriteLine("  -i, --input <path>        Path to the input HTML file");
        error.WriteLine("      --source <name>       Input source: youtube or spotify (default: youtube)");
        error.WriteLine("  -t, --token <token>       User token for ListenBrainz API");
        error.WriteLine("      --chunk-size <n>      ListenBrainz API chunk size (default: 200)");
        error.WriteLine("      --after <seconds>      Filter listens after this unix timestamp");
        error.WriteLine("      --before <seconds>     Filter listens before this unix timestamp");
        error.WriteLine("      --dry-run              Parse and display listens without submitting");
        error.WriteLine("      --skip-threshold-seconds <n>  Collapse repeated same-listen entries within <n> seconds");
        error.WriteLine("      --dedupe-window-seconds <n>  Deduplicate close consecutive listens within <n> seconds");
        error.WriteLine("      --exclude-topic-channels  Exclude \"- Topic\" channels from parsed results");
        error.WriteLine("      --audit-export <path>  Audit a ListenBrainz export JSON file instead of importing");
        error.WriteLine("      --audit-window-seconds <n>  Time window for near-duplicate detection during audit (default: 60)");
        error.WriteLine("      --output-json <path>   With --dry-run, write parsed listens to a JSON file");
        error.WriteLine("      --output-html <path>   With --dry-run, write parsed listens to a sortable HTML report");
        error.WriteLine("  -h, --help                Show this help message");
    }
}

public sealed record CommandLineParseResult(CommandLineOptions? Options, int ExitCode)
{
    public static CommandLineParseResult Success(CommandLineOptions options) => new(options, 0);

    public static CommandLineParseResult Failure(int exitCode) => new(null, exitCode);
}

public sealed record OutputOptions(string? JsonPath, string? HtmlPath);

public enum ListenSource
{
    YouTube,
    Spotify
}
