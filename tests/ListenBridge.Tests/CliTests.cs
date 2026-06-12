using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ListenBridge.Cli;
using ListenBridge.Cli.Commands;
using ListenBridge.Cli.Reports;
using ListenBridge.Core.Domain;
using ListenBridge.Core.Services;
using ListenBridge.ListenBrainz.Services;
using ListenBridge.YouTube.Services;
using Xunit;

namespace ListenBridge.Tests;

public class CliTests
{
    [Fact]
    public void CommandLineOptions_Parse_HelpReturnsSuccessExitCode()
    {
        var error = new StringWriter();
        var result = CommandLineOptions.Parse(new[] { "--help" }, error);

        Assert.Null(result.Options);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage:", error.ToString());
    }

    [Fact]
    public void CommandLineOptions_Parse_NoArgumentsReturnsFailureExitCodeWithHelp()
    {
        var error = new StringWriter();
        var result = CommandLineOptions.Parse(Array.Empty<string>(), error);

        Assert.Null(result.Options);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Usage:", error.ToString());
    }

    [Fact]
    public void CommandLineOptions_Parse_UnknownArgumentReturnsFailureExitCode()
    {
        var error = new StringWriter();
        var result = CommandLineOptions.Parse(new[] { "--unknown" }, error);

        Assert.Null(result.Options);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Unknown argument: --unknown", error.ToString());
    }

    [Fact]
    public void CommandLineOptions_Parse_InvalidIntegerReturnsFailureExitCode()
    {
        var error = new StringWriter();
        var result = CommandLineOptions.Parse(new[] { "--input", "file.html", "--chunk-size", "abc" }, error);

        Assert.Null(result.Options);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Invalid integer value for --chunk-size", error.ToString());
    }

    [Theory]
    [InlineData("--chunk-size", "0", "greater than 0")]
    [InlineData("--audit-window-seconds", "0", "greater than 0")]
    [InlineData("--skip-threshold-seconds", "-1", "0 or greater")]
    [InlineData("--dedupe-window-seconds", "-1", "0 or greater")]
    public void CommandLineOptions_Parse_InvalidNumericRangeReturnsFailureExitCode(string option, string value, string expectedMessage)
    {
        var error = new StringWriter();
        var args = option == "--audit-window-seconds"
            ? new[] { "--audit-export", "export.json", option, value }
            : new[] { "--input", "file.html", "--dry-run", option, value };

        var result = CommandLineOptions.Parse(args, error);

        Assert.Null(result.Options);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains(expectedMessage, error.ToString());
    }

    [Fact]
    public void CommandLineOptions_Parse_ValidDryRunReturnsOptions()
    {
        var error = new StringWriter();
        var result = CommandLineOptions.Parse(
            new[]
            {
                "--input", "file.html",
                "--dry-run",
                "--chunk-size", "50",
                "--skip-threshold-seconds", "0",
                "--dedupe-window-seconds", "30",
                "--exclude-topic-channels"
            },
            error);

        Assert.NotNull(result.Options);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("file.html", result.Options.InputFile);
        Assert.Equal(50, result.Options.ChunkSize);
        Assert.Null(result.Options.FilterOptions.SkipThresholdSeconds);
        Assert.Equal(30, result.Options.FilterOptions.DeduplicationWindowSeconds);
        Assert.False(result.Options.FilterOptions.IncludeTopicChannels);
    }

    [Fact]
    public void CommandLineOptions_Parse_ValidSpotifySourceReturnsOptions()
    {
        var error = new StringWriter();
        var result = CommandLineOptions.Parse(new[] { "--input", "spotify.json", "--source", "spotify", "--dry-run" }, error);

        Assert.NotNull(result.Options);
        Assert.Equal(ListenSource.Spotify, result.Options.Source);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void CommandLineOptions_Parse_InvalidSourceReturnsFailureExitCode()
    {
        var error = new StringWriter();
        var result = CommandLineOptions.Parse(new[] { "--input", "history.json", "--source", "unknown", "--dry-run" }, error);

        Assert.Null(result.Options);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Invalid source", error.ToString());
    }

    [Theory]
    [InlineData("--input")]
    [InlineData("--token")]
    [InlineData("--source")]
    [InlineData("--after")]
    [InlineData("--before")]
    [InlineData("--output-json")]
    public void CommandLineOptions_Parse_MissingOptionValueReturnsFailureExitCode(string option)
    {
        var error = new StringWriter();

        var result = CommandLineOptions.Parse(new[] { option }, error);

        Assert.Null(result.Options);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains($"Missing value for {option}", error.ToString());
    }

    [Fact]
    public void CommandLineOptions_Parse_ThrowsOnNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => CommandLineOptions.Parse(null!, new StringWriter()));
    }

    [Fact]
    public void CommandLineOptions_Parse_ThrowsOnNullErrorWriter()
    {
        Assert.Throws<ArgumentNullException>(() => CommandLineOptions.Parse(Array.Empty<string>(), null!));
    }

    [Fact]
    public void ParsedListensJsonWriter_BuildJson_ReturnsIndentedJson()
    {
        var writer = new ParsedListensJsonWriter();
        var listens = new[]
        {
            new Listen("Artist", "Track", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), new Uri("https://www.youtube.com/watch?v=abc"))
        };

        var json = writer.BuildJson(listens);
        var document = JsonDocument.Parse(json);

        Assert.True(document.RootElement.ValueKind == JsonValueKind.Array);
        Assert.Equal("Artist", document.RootElement[0].GetProperty("ArtistName").GetString());
    }

    [Fact]
    public void ParsedListensHtmlReportWriter_BuildHtmlReport_EncodesUnsafeCharacters()
    {
        var writer = new ParsedListensHtmlReportWriter();
        var listens = new[]
        {
            new Listen("Artist <X>", "Track & More", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), new Uri("https://www.youtube.com/watch?v=abc"))
        };

        var html = writer.BuildHtmlReport(listens, "source.html");

        Assert.Contains("Artist &lt;X&gt;", html);
        Assert.Contains("Track &amp; More", html);
    }

    [Fact]
    public void ParsedListensHtmlReportWriter_BuildHtmlReport_SuppressesNonWebOriginLinksAndEncodesSourcePath()
    {
        var writer = new ParsedListensHtmlReportWriter();
        var listens = new[]
        {
            new Listen("Artist", "Unsafe Origin", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), new Uri("file:///C:/temp/listen.html")),
            new Listen("Artist", "Safe Origin", new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero), new Uri("https://example.com/watch?v=1&list=2"))
        };

        var html = writer.BuildHtmlReport(listens, "source <file>&.html");

        Assert.Contains("source &lt;file&gt;&amp;.html", html);
        Assert.DoesNotContain("file:///C:/temp/listen.html", html);
        Assert.Contains("https://example.com/watch?v=1&amp;list=2", html);
        Assert.Contains("<a href=\"https://example.com/watch?v=1&amp;list=2\" target=\"_blank\">", html);
    }

    [Fact]
    public async Task ImportCommand_DryRun_WritesOutputAndReports()
    {
        var tempInput = Path.GetTempFileName();
        var jsonPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        var htmlPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".html");

        try
        {
            await File.WriteAllTextAsync(tempInput, "<html></html>", Encoding.UTF8);
            var options = new CommandLineOptions(
                InputFile: tempInput,
                Source: ListenSource.YouTube,
                UserToken: string.Empty,
                ChunkSize: 200,
                After: DateTimeOffset.MinValue,
                Before: DateTimeOffset.MaxValue,
                DryRun: true,
                Output: new OutputOptions(jsonPath, htmlPath),
                FilterOptions: new YouTubeListenFilterOptions(),
                AuditExportPath: null,
                AuditWindowSeconds: 60);

            var parser = new FakeParser(new[]
            {
                new Listen("Artist", "Track", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), new Uri("https://www.youtube.com/watch?v=abc"))
            });

            var output = new StringWriter();
            var error = new StringWriter();
            var command = new ImportCommand(parser, _ => new FakeClient(), new ParsedListensJsonWriter(), new ParsedListensHtmlReportWriter());

            var exitCode = await command.ExecuteAsync(options, output, error);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(jsonPath));
            Assert.True(File.Exists(htmlPath));
            Assert.Contains("Parsed 1 rows", output.ToString());
        }
        finally
        {
            File.Delete(tempInput);
            if (File.Exists(jsonPath)) File.Delete(jsonPath);
            if (File.Exists(htmlPath)) File.Delete(htmlPath);
        }
    }

    [Fact]
    public async Task AuditCommand_ProducesSummary()
    {
        var tempAudit = Path.GetTempFileName();
        try
        {
            var listens = new[]
            {
                new
                {
                    listened_at = 1700000000L,
                    track_metadata = new { artist_name = "Artist A", track_name = "Song A" }
                }
            };

            await File.WriteAllTextAsync(tempAudit, JsonSerializer.Serialize(listens), Encoding.UTF8);

            var options = new CommandLineOptions(
                InputFile: string.Empty,
                Source: ListenSource.YouTube,
                UserToken: string.Empty,
                ChunkSize: 200,
                After: DateTimeOffset.MinValue,
                Before: DateTimeOffset.MaxValue,
                DryRun: false,
                Output: new OutputOptions(null, null),
                FilterOptions: new YouTubeListenFilterOptions(),
                AuditExportPath: tempAudit,
                AuditWindowSeconds: 60);

            var output = new StringWriter();
            var error = new StringWriter();
            var command = new AuditCommand(new ListenBrainzAuditService(60));

            var exitCode = await command.ExecuteAsync(options, output, error);

            Assert.Equal(0, exitCode);
            Assert.Contains("ListenBrainz Audit Results", output.ToString());
            Assert.Contains("Total listens: 1", output.ToString());
        }
        finally
        {
            File.Delete(tempAudit);
        }
    }

    [Fact]
    public async Task ImportCommand_DryRun_CreatesParentDirectoriesForReports()
    {
        var tempInput = Path.GetTempFileName();
        var outputRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var jsonPath = Path.Combine(outputRoot, "json", "listens.json");
        var htmlPath = Path.Combine(outputRoot, "html", "listens.html");

        try
        {
            await File.WriteAllTextAsync(tempInput, "<html></html>", Encoding.UTF8);
            var options = new CommandLineOptions(
                InputFile: tempInput,
                Source: ListenSource.YouTube,
                UserToken: string.Empty,
                ChunkSize: 200,
                After: DateTimeOffset.MinValue,
                Before: DateTimeOffset.MaxValue,
                DryRun: true,
                Output: new OutputOptions(jsonPath, htmlPath),
                FilterOptions: new YouTubeListenFilterOptions(),
                AuditExportPath: null,
                AuditWindowSeconds: 60);
            var parser = new FakeParser(new[]
            {
                new Listen("Artist", "Track", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), new Uri("https://www.youtube.com/watch?v=abc"))
            });
            var command = new ImportCommand(parser, _ => new FakeClient(), new ParsedListensJsonWriter(), new ParsedListensHtmlReportWriter());

            var exitCode = await command.ExecuteAsync(options, new StringWriter(), new StringWriter());

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(jsonPath));
            Assert.True(File.Exists(htmlPath));
        }
        finally
        {
            File.Delete(tempInput);
            if (Directory.Exists(outputRoot)) Directory.Delete(outputRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Program_RunAsync_MalformedAuditJsonReturnsCleanError()
    {
        var tempAudit = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempAudit, "not-json", Encoding.UTF8);

            var output = new StringWriter();
            var error = new StringWriter();

            var exitCode = await Program.RunAsync(new[] { "--audit-export", tempAudit }, output, error);

            Assert.Equal(1, exitCode);
            Assert.Contains("Invalid JSON:", error.ToString());
            Assert.DoesNotContain(" at ", error.ToString());
        }
        finally
        {
            File.Delete(tempAudit);
        }
    }

    [Fact]
    public async Task Program_RunAsync_ReportWriteFailureReturnsCleanError()
    {
        var tempInput = Path.GetTempFileName();
        var directoryPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(directoryPath);

        try
        {
            await File.WriteAllTextAsync(tempInput, "<html></html>", Encoding.UTF8);

            var output = new StringWriter();
            var error = new StringWriter();

            var exitCode = await Program.RunAsync(
                new[] { "--input", tempInput, "--dry-run", "--output-json", directoryPath },
                output,
                error);

            Assert.Equal(1, exitCode);
            Assert.Contains("File access error:", error.ToString());
            Assert.DoesNotContain(" at ", error.ToString());
        }
        finally
        {
            File.Delete(tempInput);
            Directory.Delete(directoryPath);
        }
    }


    private sealed class FakeParser : IYouTubeTakeoutParser
    {
        private readonly IReadOnlyList<Listen> _listens;

        public FakeParser(IReadOnlyList<Listen> listens)
        {
            _listens = listens;
        }

        public IReadOnlyList<Listen> Parse(string htmlContent) => _listens;

        public YouTubeParseResult ParseWithDiagnostics(string htmlContent, YouTubeListenFilterOptions? filterOptions = null)
        {
            var filtered = filterOptions is null ? _listens : YouTubeListenFilter.Apply(_listens, filterOptions);
            return new YouTubeParseResult
            {
                Listens = filtered,
                TotalRows = _listens.Count
            };
        }

        ListenParseResult IListenSourceParser.ParseWithDiagnostics(string content, YouTubeListenFilterOptions? filterOptions)
        {
            return ParseWithDiagnostics(content, filterOptions);
        }
    }

    private sealed class FakeClient : IListenBrainzClient
    {
        public Task SendListensAsync(IEnumerable<Listen> listens, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

}
