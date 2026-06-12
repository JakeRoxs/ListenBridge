namespace ListenBridge.Core.Services;

public sealed class ListenImportPipeline
{
    private readonly IListenSourceParser _parser;
    private readonly IScrobbleDestination _destination;

    public ListenImportPipeline(IListenSourceParser parser, IScrobbleDestination destination)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _destination = destination ?? throw new ArgumentNullException(nameof(destination));
    }

    public static ImportResult PrepareImportResult(IListenSourceParser parser, string content, DateTimeOffset after, DateTimeOffset before, YouTubeListenFilterOptions? filterOptions = null)
    {
        if (parser is null)
        {
            throw new ArgumentNullException(nameof(parser));
        }

        if (content is null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        var parseResult = parser.ParseWithDiagnostics(content, filterOptions);
        var listens = parseResult.Listens
            .Where(listen => listen.ListenedAt > after && listen.ListenedAt < before)
            .ToList();

        return new ImportResult(parseResult, listens);
    }

    public async Task<ImportResult> ImportAsync(string content, DateTimeOffset after, DateTimeOffset before, YouTubeListenFilterOptions? filterOptions = null, CancellationToken cancellationToken = default)
    {
        var result = PrepareImportResult(_parser, content, after, before, filterOptions);

        if (result.ListenCount > 0)
        {
            await _destination.SendListensAsync(result.Listens, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
}
