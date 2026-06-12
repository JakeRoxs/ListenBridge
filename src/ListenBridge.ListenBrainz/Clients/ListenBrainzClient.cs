using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ListenBridge.Core.Domain;
using ListenBridge.Core.Services;
using ListenBridge.ListenBrainz.Models;

namespace ListenBridge.ListenBrainz.Clients;

public sealed class ListenBrainzClient : IListenBrainzClient, IDisposable
{
    private const string SubmitEndpoint = "1/submit-listens";
    private const string DefaultBaseAddress = "https://api.listenbrainz.org/";
    private readonly HttpClient _httpClient;
    private readonly string _token;
    private readonly int _chunkSize;
    private readonly TimeSpan _delayBetweenChunks;
    private readonly int _maxRetries;
    private readonly TimeSpan _retryDelay;
    private readonly bool _ownsHttpClient;
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public ListenBrainzClient(HttpClient httpClient, string token, int chunkSize = 200, TimeSpan? delayBetweenChunks = null, int maxRetries = 3, TimeSpan? retryDelay = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _token = token ?? throw new ArgumentNullException(nameof(token));
        _chunkSize = chunkSize > 0 ? chunkSize : throw new ArgumentOutOfRangeException(nameof(chunkSize));
        _delayBetweenChunks = delayBetweenChunks ?? TimeSpan.FromSeconds(5);
        _maxRetries = maxRetries >= 0 ? maxRetries : throw new ArgumentOutOfRangeException(nameof(maxRetries));
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(2);
        _ownsHttpClient = false;

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = new Uri(DefaultBaseAddress, UriKind.Absolute);
        }
    }

    public ListenBrainzClient(string token, int chunkSize = 200, TimeSpan? delayBetweenChunks = null, HttpMessageHandler? handler = null, Uri? baseAddress = null, int maxRetries = 3, TimeSpan? retryDelay = null)
        : this(handler is null ? new HttpClient() : new HttpClient(handler), token, chunkSize, delayBetweenChunks, maxRetries, retryDelay)
    {
        _ownsHttpClient = true;
        _httpClient.BaseAddress = baseAddress ?? new Uri(DefaultBaseAddress, UriKind.Absolute);
    }

    public async Task SendListensAsync(IEnumerable<Listen> listens, CancellationToken cancellationToken = default)
    {
        if (listens is null)
        {
            throw new ArgumentNullException(nameof(listens));
        }

        var payloads = listens.Select(ListenBrainzPayload.FromDomain).ToList();
        if (payloads.Count == 0)
        {
            return;
        }

        for (var chunkIndex = 0; chunkIndex < payloads.Count; chunkIndex += _chunkSize)
        {
            var chunk = payloads.Skip(chunkIndex).Take(_chunkSize).ToList();
            var requestJson = JsonSerializer.Serialize(new ListenBrainzListens { Payload = chunk }, JsonOptions);
            await SendChunkAsync(requestJson, cancellationToken).ConfigureAwait(false);

            if (chunkIndex + _chunkSize < payloads.Count)
            {
                await Task.Delay(_delayBetweenChunks, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task SendChunkAsync(string requestJson, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                using var requestMessage = CreateRequest(requestJson);
                using var response = await _httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    return;
                }

                if (!IsTransientStatusCode(response.StatusCode) || attempt >= _maxRetries)
                {
                    throw new HttpRequestException(
                        $"ListenBrainz submission failed with {(int)response.StatusCode} {response.ReasonPhrase}: {responseBody}");
                }

                await DelayBeforeRetryAsync(response.Headers.RetryAfter, attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException) when (attempt < _maxRetries)
            {
                await DelayBeforeRetryAsync(null, attempt, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private HttpRequestMessage CreateRequest(string requestJson)
    {
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, SubmitEndpoint)
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };

        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Token", _token);
        return requestMessage;
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;
    }

    private Task DelayBeforeRetryAsync(RetryConditionHeaderValue? retryAfter, int attempt, CancellationToken cancellationToken)
    {
        var delay = GetRetryDelay(retryAfter, attempt);
        return delay > TimeSpan.Zero ? Task.Delay(delay, cancellationToken) : Task.CompletedTask;
    }

    private TimeSpan GetRetryDelay(RetryConditionHeaderValue? retryAfter, int attempt)
    {
        if (retryAfter?.Delta is TimeSpan delta && delta > TimeSpan.Zero)
        {
            return delta;
        }

        if (retryAfter?.Date is DateTimeOffset date)
        {
            var delay = date - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                return delay;
            }
        }

        var multiplier = Math.Pow(2, attempt);
        return TimeSpan.FromMilliseconds(_retryDelay.TotalMilliseconds * multiplier);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_ownsHttpClient)
            {
                _httpClient.Dispose();
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
