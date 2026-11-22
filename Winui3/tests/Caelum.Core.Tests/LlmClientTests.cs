using System.Net;
using System.Text;
using Caelum.Core.Domain;
using Caelum.Core.Services;
using Xunit;

namespace Caelum.Core.Tests;

/// <summary>
///     Exercises the OpenAI compatible client against a fake transport, so the request shape,
///     streaming parser and error mapping are verified without a network.
/// </summary>
public sealed class LlmClientTests
{
    private const string BaseUrl = "https://api.longcat.chat/openai";
    private const string ApiKey = "test-key";
    private const string Model = "LongCat-2.5";

    [Fact]
    public void LongCatPresetMatchesTheDocumentedEndpoint()
    {
        Assert.Equal("https://api.longcat.chat/openai", LlmProviderPresets.LongCat.DefaultBaseUrl);
        Assert.Equal("LongCat-2.5", LlmProviderPresets.LongCat.DefaultModel);
    }

    [Theory]
    [InlineData("https://api.longcat.chat/openai", "https://api.longcat.chat/openai/v1/chat/completions")]
    [InlineData("https://api.longcat.chat/openai/", "https://api.longcat.chat/openai/v1/chat/completions")]
    [InlineData("https://api.longcat.chat/openai/v1", "https://api.longcat.chat/openai/v1/chat/completions")]
    [InlineData("https://api.longcat.chat/openai/v1/chat/completions", "https://api.longcat.chat/openai/v1/chat/completions")]
    [InlineData("https://api.openai.com", "https://api.openai.com/v1/chat/completions")]
    public void BuildChatUrlAcceptsEveryBaseUrlShape(string baseUrl, string expected)
    {
        Assert.Equal(expected, OpenAiCompatibleLlmClient.BuildChatUrl(baseUrl));
    }

    [Fact]
    public async Task CompleteAsyncSendsTheBearerTokenAndModel()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponseFactory.Completion("hello"));

        using var client = new OpenAiCompatibleLlmClient(handler);

        var result = await client.CompleteAsync(
            BaseUrl,
            ApiKey,
            Model,
            [new LlmMessage("user", "Hi")],
            stream: false);

        Assert.True(result.IsSuccess);
        Assert.Equal("hello", result.Content);
        Assert.Equal(
            "https://api.longcat.chat/openai/v1/chat/completions",
            handler.LastRequest?.RequestUri?.ToString());
        Assert.Equal("Bearer test-key", handler.LastRequest?.Headers.Authorization?.ToString());
        Assert.Contains("\"model\":\"LongCat-2.5\"", handler.LastBody);
    }

    [Fact]
    public async Task CompleteAsyncParsesAStreamedReply()
    {
        var sse = string.Join(
            "\n",
            "data: {\"choices\":[{\"delta\":{\"content\":\"Hel\"}}]}",
            "data: {\"choices\":[{\"delta\":{\"content\":\"lo\"}}]}",
            "data: [DONE]",
            string.Empty);

        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(sse, Encoding.UTF8, "text/event-stream")
        });

        using var client = new OpenAiCompatibleLlmClient(handler);

        var progress = new CollectingProgress();

        var result = await client.CompleteAsync(
            BaseUrl,
            ApiKey,
            Model,
            [new LlmMessage("user", "Hi")],
            progress: progress);

        Assert.True(result.IsSuccess);
        Assert.Equal("Hello", result.Content);
        Assert.Equal(["Hel", "lo"], progress.Deltas);
    }

    [Fact]
    public async Task CompleteAsyncAcceptsAPlainJsonReplyToAStreamingRequest()
    {
        // A few gateways ignore the stream flag and answer with a normal completion object
        var handler = new FakeHttpMessageHandler(_ => JsonResponseFactory.Completion("plain"));

        using var client = new OpenAiCompatibleLlmClient(handler);

        var result = await client.CompleteAsync(
            BaseUrl,
            ApiKey,
            Model,
            [new LlmMessage("user", "Hi")]);

        Assert.True(result.IsSuccess);
        Assert.Equal("plain", result.Content);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, LlmErrorKind.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, LlmErrorKind.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound, LlmErrorKind.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests, LlmErrorKind.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, LlmErrorKind.Unknown)]
    public async Task CompleteAsyncMapsHttpErrors(HttpStatusCode status, LlmErrorKind expected)
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent("error")
        });

        using var client = new OpenAiCompatibleLlmClient(handler);

        var result = await client.CompleteAsync(
            BaseUrl,
            ApiKey,
            Model,
            [new LlmMessage("user", "Hi")]);

        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.ErrorKind);
    }

    [Fact]
    public async Task CompleteAsyncMapsANetworkFailure()
    {
        using var client = new OpenAiCompatibleLlmClient(new ThrowingHttpMessageHandler());

        var result = await client.CompleteAsync(
            BaseUrl,
            ApiKey,
            Model,
            [new LlmMessage("user", "Hi")]);

        Assert.False(result.IsSuccess);
        Assert.Equal(LlmErrorKind.Network, result.ErrorKind);
    }

    [Fact]
    public async Task TestConnectionAsyncSendsANonStreamingRequest()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponseFactory.Completion("ok"));

        using var client = new OpenAiCompatibleLlmClient(handler);

        var result = await client.TestConnectionAsync(BaseUrl, ApiKey, Model);

        Assert.True(result.IsSuccess);
        Assert.Contains("\"stream\":false", handler.LastBody);
    }

    private sealed class CollectingProgress : IProgress<string>
    {
        private readonly List<string> _deltas = [];

        public IReadOnlyList<string> Deltas => _deltas;

        public void Report(string value) => _deltas.Add(value);
    }
}
