using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Caelum.Core.Services;

/// <summary>
///     Talks to an OpenAI compatible chat completion endpoint. LongCat, OpenAI itself and most self
///     hosted gateways expose the same contract, so one client covers every provider the settings
///     page offers.
/// </summary>
public sealed class OpenAiCompatibleLlmClient : IDisposable
{
    /// <summary>Upper bound on a whole request, including reading a streamed answer.</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(2);

    /// <summary>Tokens requested for a normal reply. LongCat allows up to 256K, so this is a safe ceiling.</summary>
    private const int MaxTokens = 2048;

    /// <summary>Tokens requested when testing a configuration, where the content does not matter.</summary>
    private const int TestMaxTokens = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;

    /// <summary>Creates a client. Tests pass their own handler to inspect or fake the transport.</summary>
    public OpenAiCompatibleLlmClient(HttpMessageHandler? handler = null)
    {
        _httpClient = new HttpClient(handler ?? new HttpClientHandler())
        {
            Timeout = RequestTimeout
        };
    }

    /// <summary>
    ///     Sends a chat completion request. Streaming is requested by default, and the client also
    ///     accepts a plain JSON answer, because a few gateways ignore <c>stream</c>.
    /// </summary>
    public async Task<LlmResult> CompleteAsync(
        string baseUrl,
        string apiKey,
        string model,
        IReadOnlyList<LlmMessage> messages,
        bool stream = true,
        int maxTokens = MaxTokens,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildChatUrl(baseUrl))
        {
            Content = JsonContent.Create(new ChatCompletionRequest(
                model,
                messages.Select(message => new LlmMessageContent(message.Role, message.Content)).ToList(),
                stream,
                maxTokens))
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        return await SendAsync(request, stream, progress, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sends the smallest possible completion to prove the key, base URL and model name are
    ///     accepted, without spending a meaningful number of tokens.
    /// </summary>
    public Task<LlmResult> TestConnectionAsync(
        string baseUrl,
        string apiKey,
        string model,
        CancellationToken cancellationToken = default) =>
        CompleteAsync(
            baseUrl,
            apiKey,
            model,
            [new LlmMessage("user", "Hi")],
            stream: false,
            maxTokens: TestMaxTokens,
            progress: null,
            cancellationToken: cancellationToken);

    /// <summary>
    ///     Turns the stored base URL into the chat completion endpoint. The preset stores the root
    ///     (<c>https://api.longcat.chat/openai</c>), while a student may paste either the root, the
    ///     <c>/v1</c> root or the full endpoint, so all three are accepted.
    /// </summary>
    internal static string BuildChatUrl(string baseUrl)
    {
        var trimmed = baseUrl.Trim().TrimEnd('/');

        if (trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            return trimmed + "/chat/completions";

        return trimmed + "/v1/chat/completions";
    }

    private async Task<LlmResult> SendAsync(
        HttpRequestMessage request,
        bool stream,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await _httpClient
                .SendAsync(
                    request,
                    stream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return LlmResult.Failure(LlmErrorKind.Cancelled);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return LlmResult.Failure(LlmErrorKind.Network, ex.Message);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                return await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);

            var contentType = response.Content.Headers.ContentType?.MediaType;

            if (contentType == "text/event-stream")
                return await ReadEventStreamAsync(response, progress, cancellationToken).ConfigureAwait(false);

            return await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<LlmResult> ReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var kind = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => LlmErrorKind.Unauthorized,
            HttpStatusCode.NotFound => LlmErrorKind.NotFound,
            HttpStatusCode.TooManyRequests => LlmErrorKind.RateLimited,
            _ => LlmErrorKind.Unknown
        };

        string? detail = null;

        try
        {
            detail = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The status code already says enough; a body that cannot be read is not worth failing over
        }

        return LlmResult.Failure(kind, detail);
    }

    private static async Task<LlmResult> ReadJsonAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        string body;

        try
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return LlmResult.Failure(LlmErrorKind.Cancelled);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or IOException)
        {
            return LlmResult.Failure(LlmErrorKind.Network, ex.Message);
        }

        try
        {
            var completion = JsonSerializer.Deserialize<ChatCompletionResponse>(body, JsonOptions);
            var content = completion?.Choices?.ElementAtOrDefault(0)?.Message?.Content;

            return LlmResult.Success(content ?? string.Empty);
        }
        catch (JsonException ex)
        {
            return LlmResult.Failure(LlmErrorKind.Unknown, ex.Message);
        }
    }

    private static async Task<LlmResult> ReadEventStreamAsync(
        HttpResponseMessage response,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();

        // HttpClient.Timeout covers the response headers, not the stream that follows, so the read
        // gets its own deadline linked to the caller's cancellation
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        var token = timeout.Token;

        try
        {
            var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);

            using var reader = new StreamReader(stream, Encoding.UTF8);

            while (true)
            {
                var line = await reader.ReadLineAsync(token).ConfigureAwait(false);

                if (line is null) break;

                if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

                var data = line["data:".Length..].Trim();

                if (data.Length == 0) continue;

                if (data == "[DONE]") break;

                ChatCompletionResponse? chunk;

                try
                {
                    chunk = JsonSerializer.Deserialize<ChatCompletionResponse>(data, JsonOptions);
                }
                catch (JsonException)
                {
                    // A malformed line must not take the whole reply down; the next event may still be valid
                    continue;
                }

                if (chunk?.Error is not null)
                    return LlmResult.Failure(LlmErrorKind.Unknown, chunk.Error.Message);

                var delta = chunk?.Choices?.ElementAtOrDefault(0)?.Delta?.Content;

                if (string.IsNullOrEmpty(delta)) continue;

                builder.Append(delta);
                progress?.Report(delta);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return LlmResult.Failure(LlmErrorKind.Cancelled);
        }
        catch (OperationCanceledException ex)
        {
            // The read deadline expired before the answer finished
            return LlmResult.Failure(LlmErrorKind.Network, ex.Message, builder.ToString());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or IOException or JsonException)
        {
            // A dropped connection mid answer still leaves whatever arrived, so the caller can show it
            return LlmResult.Failure(LlmErrorKind.Network, ex.Message, builder.ToString());
        }

        return LlmResult.Success(builder.ToString());
    }

    /// <inheritdoc />
    public void Dispose() => _httpClient.Dispose();

    private sealed record ChatCompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<LlmMessageContent> Messages,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("max_tokens")] int MaxTokens);

    private sealed record LlmMessageContent(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatCompletionResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<ChatChoice>? Choices,
        [property: JsonPropertyName("error")] ChatError? Error);

    private sealed record ChatChoice(
        [property: JsonPropertyName("message")] ChatMessageContent? Message,
        [property: JsonPropertyName("delta")] ChatMessageContent? Delta);

    private sealed record ChatMessageContent(
        [property: JsonPropertyName("content")] string? Content);

    private sealed record ChatError(
        [property: JsonPropertyName("message")] string? Message);
}
