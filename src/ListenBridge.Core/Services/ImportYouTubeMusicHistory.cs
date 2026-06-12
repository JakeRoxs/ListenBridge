using ListenBridge.Core.Domain;

namespace ListenBridge.Core.Services;

public sealed class ImportYouTubeMusicHistory
{
    private readonly ListenImportPipeline _pipeline;

    public ImportYouTubeMusicHistory(IYouTubeTakeoutParser parser, IListenBrainzClient client)
    {
        _pipeline = new ListenImportPipeline(parser, client);
    }

    public static ImportResult PrepareImportResult(IYouTubeTakeoutParser parser, string htmlContent, DateTimeOffset after, DateTimeOffset before, YouTubeListenFilterOptions? filterOptions = null)
    {
        return ListenImportPipeline.PrepareImportResult(parser, htmlContent, after, before, filterOptions);
    }

    public async Task<ImportResult> ImportAsync(string htmlContent, DateTimeOffset after, DateTimeOffset before, YouTubeListenFilterOptions? filterOptions = null, CancellationToken cancellationToken = default)
    {
        return await _pipeline.ImportAsync(htmlContent, after, before, filterOptions, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class ImportResult
{
    public ListenParseResult ParseResult { get; }
    public IReadOnlyList<Listen> Listens { get; }
    public int ListenCount => Listens.Count;
    public int DateFilteredRows => ParseResult.Listens.Count - ListenCount;

    public ImportResult(ListenParseResult parseResult, IReadOnlyList<Listen> listens)
    {
        ParseResult = parseResult ?? throw new ArgumentNullException(nameof(parseResult));
        Listens = listens ?? throw new ArgumentNullException(nameof(listens));
    }
}
