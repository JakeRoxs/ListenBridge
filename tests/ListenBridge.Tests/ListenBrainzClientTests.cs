using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ListenBridge.Core.Domain;
using ListenBridge.ListenBrainz.Clients;
using Xunit;

namespace ListenBridge.Tests;

public class ListenBrainzClientTests
{
    [Fact]
    public async Task SendListensAsync_SendsSnakeCaseJsonAndAuthorizationHeader()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, "{\"status\":\"ok\"}");
        using var client = new ListenBrainzClient("user-token", chunkSize: 1, delayBetweenChunks: TimeSpan.Zero, handler: handler);

        var listens = new[]
        {
            new Listen("Artist", "Song", new DateTimeOffset(2023, 4, 14, 16, 20, 0, TimeSpan.Zero), new Uri("https://example.com/watch?v=1"))
        };

        await client.SendListensAsync(listens);

        Assert.Single(handler.Requests);
        var request = handler.Requests[0];

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("Token", request.AuthorizationScheme);
        Assert.Equal("user-token", request.AuthorizationParameter);
        Assert.Equal("application/json", request.ContentType);

        var payload = JsonSerializer.Deserialize<JsonElement>(request.ContentBody);

        Assert.True(payload.TryGetProperty("payload", out var payloadArray));
        Assert.Equal(JsonValueKind.Array, payloadArray.ValueKind);
        Assert.Equal(1, payloadArray.GetArrayLength());
        var first = payloadArray[0];
        Assert.Equal("import", payload.GetProperty("listen_type").GetString());
        Assert.Equal(new DateTimeOffset(2023, 4, 14, 16, 20, 0, TimeSpan.Zero).ToUnixTimeSeconds(), first.GetProperty("listened_at").GetInt64());
        Assert.Equal("Artist", first.GetProperty("track_metadata").GetProperty("artist_name").GetString());
        Assert.Equal("Song", first.GetProperty("track_metadata").GetProperty("track_name").GetString());
        var additionalInfo = first.GetProperty("track_metadata").GetProperty("additional_info");
        Assert.Equal("https://example.com/watch?v=1", additionalInfo.GetProperty("origin_url").GetString());
        Assert.Equal("example.com", additionalInfo.GetProperty("music_service").GetString());
        Assert.Equal("YouTube Music", additionalInfo.GetProperty("media_player").GetString());
        Assert.Equal("ListenBridge", additionalInfo.GetProperty("submission_client").GetString());
        Assert.False(string.IsNullOrWhiteSpace(additionalInfo.GetProperty("submission_client_version").GetString()));
    }

    [Fact]
    public async Task SendListensAsync_WithExternalHttpClient_UsesCustomBaseAddressAndDoesNotDisposeExternalClient()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, "{\"status\":\"ok\"}");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://custom.test/") };
        using var client = new ListenBrainzClient(httpClient, "user-token", chunkSize: 1, delayBetweenChunks: TimeSpan.Zero);

        var listens = new[]
        {
            new Listen("Artist", "Song", new DateTimeOffset(2023, 4, 14, 16, 20, 0, TimeSpan.Zero), new Uri("https://example.com/watch?v=1"))
        };

        await client.SendListensAsync(listens);
        Assert.Single(handler.Requests);
        Assert.Equal(new Uri("https://custom.test/1/submit-listens"), handler.Requests[0].RequestUri);

        client.Dispose();
        var response = await httpClient.GetAsync(new Uri("status", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SendListensAsync_ThrowsHttpRequestException_OnNonSuccessResponse()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.InternalServerError, "{\"error\":\"fail\"}");
        using var client = new ListenBrainzClient("user-token", chunkSize: 1, delayBetweenChunks: TimeSpan.Zero, handler: handler, maxRetries: 0, retryDelay: TimeSpan.Zero);

        var listens = new[]
        {
            new Listen("Artist", "Song", new DateTimeOffset(2023, 4, 14, 16, 20, 0, TimeSpan.Zero), new Uri("https://example.com/watch?v=1"))
        };

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendListensAsync(listens));
    }

    [Fact]
    public async Task SendListensAsync_RetriesTransientServerErrorThenSucceeds()
    {
        var handler = new SequenceHttpMessageHandler(
            Response(HttpStatusCode.InternalServerError, "{\"error\":\"fail\"}"),
            Response(HttpStatusCode.OK, "{\"status\":\"ok\"}"));
        using var client = new ListenBrainzClient("user-token", chunkSize: 1, delayBetweenChunks: TimeSpan.Zero, handler: handler, retryDelay: TimeSpan.Zero);

        await client.SendListensAsync(SingleListen());

        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task SendListensAsync_HonorsRetryAfterForRateLimitResponse()
    {
        var rateLimit = Response(HttpStatusCode.TooManyRequests, "rate limited");
        rateLimit.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(1));
        var handler = new SequenceHttpMessageHandler(rateLimit, Response(HttpStatusCode.OK, "{\"status\":\"ok\"}"));
        using var client = new ListenBrainzClient("user-token", chunkSize: 1, delayBetweenChunks: TimeSpan.Zero, handler: handler, retryDelay: TimeSpan.FromSeconds(30));

        await client.SendListensAsync(SingleListen());

        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task SendListensAsync_RetriesNetworkExceptionThenSucceeds()
    {
        var handler = new SequenceHttpMessageHandler(
            new HttpRequestException("network unavailable"),
            Response(HttpStatusCode.OK, "{\"status\":\"ok\"}"));
        using var client = new ListenBrainzClient("user-token", chunkSize: 1, delayBetweenChunks: TimeSpan.Zero, handler: handler, retryDelay: TimeSpan.Zero);

        await client.SendListensAsync(SingleListen());

        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task SendListensAsync_RetryExhaustionThrowsClearHttpRequestException()
    {
        var handler = new SequenceHttpMessageHandler(
            Response(HttpStatusCode.ServiceUnavailable, "first"),
            Response(HttpStatusCode.ServiceUnavailable, "second"));
        using var client = new ListenBrainzClient("user-token", chunkSize: 1, delayBetweenChunks: TimeSpan.Zero, handler: handler, maxRetries: 1, retryDelay: TimeSpan.Zero);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.SendListensAsync(SingleListen()));

        Assert.Equal(2, handler.RequestCount);
        Assert.Contains("ListenBrainz submission failed", exception.Message);
        Assert.Contains("503", exception.Message);
    }

    [Fact]
    public async Task SendListensAsync_CancellationStopsRetryDelay()
    {
        var handler = new SequenceHttpMessageHandler(Response(HttpStatusCode.ServiceUnavailable, "retry later"));
        using var client = new ListenBrainzClient("user-token", chunkSize: 1, delayBetweenChunks: TimeSpan.Zero, handler: handler, maxRetries: 3, retryDelay: TimeSpan.FromSeconds(30));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(10));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SendListensAsync(SingleListen(), cts.Token));

        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task SendListensAsync_ThrowsTaskCanceledException_WhenCancellationRequested()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, "{\"status\":\"ok\"}");
        using var client = new ListenBrainzClient("user-token", chunkSize: 1, delayBetweenChunks: TimeSpan.Zero, handler: handler);
        var listens = new[]
        {
            new Listen("Artist", "Song", new DateTimeOffset(2023, 4, 14, 16, 20, 0, TimeSpan.Zero), new Uri("https://example.com/watch?v=1"))
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(() => client.SendListensAsync(listens, cts.Token));
    }

    [Fact]
    public void Constructor_ThrowsOnInvalidChunkSizeWithHttpClient()
    {
        using var httpClient = new HttpClient();

        Assert.Throws<ArgumentOutOfRangeException>(() => new ListenBrainzClient(httpClient, "user-token", chunkSize: 0));
    }

    [Fact]
    public void Constructor_ThrowsOnNullHttpClient()
    {
        Assert.Throws<ArgumentNullException>(() => new ListenBrainzClient(null!, "user-token"));
    }

    [Fact]
    public void Constructor_ThrowsOnInvalidMaxRetries()
    {
        using var httpClient = new HttpClient();

        Assert.Throws<ArgumentOutOfRangeException>(() => new ListenBrainzClient(httpClient, "user-token", maxRetries: -1));
    }

    [Fact]
    public async Task SendListensAsync_DoesNothingForEmptyListenCollection()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, "{\"status\":\"ok\"}");
        using var client = new ListenBrainzClient("user-token", chunkSize: 1, delayBetweenChunks: TimeSpan.Zero, handler: handler);

        await client.SendListensAsync(Array.Empty<Listen>());

        Assert.Empty(handler.Requests);
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = new();
        private readonly HttpResponseMessage _response;

        public RecordingHttpMessageHandler(HttpStatusCode statusCode, string responseBody)
        {
            _response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new TaskCanceledException();
            }

            var contentBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            Requests.Add(new CapturedRequest
            {
                Method = request.Method,
                RequestUri = request.RequestUri,
                AuthorizationScheme = request.Headers.Authorization?.Scheme ?? string.Empty,
                AuthorizationParameter = request.Headers.Authorization?.Parameter ?? string.Empty,
                ContentType = request.Content?.Headers.ContentType?.MediaType ?? string.Empty,
                ContentBody = contentBody
            });

            return new HttpResponseMessage(_response.StatusCode)
            {
                Content = new StringContent(await _response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false), Encoding.UTF8, "application/json")
            };
        }

        public sealed class CapturedRequest
        {
            public HttpMethod Method { get; set; } = HttpMethod.Get;
            public Uri? RequestUri { get; set; }
            public string AuthorizationScheme { get; set; } = string.Empty;
            public string AuthorizationParameter { get; set; } = string.Empty;
            public string ContentType { get; set; } = string.Empty;
            public string ContentBody { get; set; } = string.Empty;
        }
    }

    private static IReadOnlyList<Listen> SingleListen()
    {
        return new[]
        {
            new Listen("Artist", "Song", new DateTimeOffset(2023, 4, 14, 16, 20, 0, TimeSpan.Zero), new Uri("https://example.com/watch?v=1"))
        };
    }

    private static HttpResponseMessage Response(HttpStatusCode statusCode, string content)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };
    }

    private sealed class SequenceHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<object> _responses;

        public SequenceHttpMessageHandler(params object[] responses)
        {
            _responses = new Queue<object>(responses);
        }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;

            if (cancellationToken.IsCancellationRequested)
            {
                throw new TaskCanceledException();
            }

            if (_responses.Count == 0)
            {
                return Task.FromResult(Response(HttpStatusCode.OK, "{\"status\":\"ok\"}"));
            }

            var next = _responses.Dequeue();
            if (next is HttpRequestException exception)
            {
                throw exception;
            }

            return Task.FromResult((HttpResponseMessage)next);
        }
    }
}
