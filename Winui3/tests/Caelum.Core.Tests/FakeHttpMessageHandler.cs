using System.Net;
using System.Text;
using System.Text.Json;

namespace Caelum.Core.Tests;

/// <summary>A fake transport that captures the request and replies with a canned response.</summary>
internal sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    public string? LastBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;

        if (request.Content is not null)
            LastBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        return respond(request);
    }
}

/// <summary>A transport that always fails, standing in for an unreachable network.</summary>
internal sealed class ThrowingHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Task.FromException<HttpResponseMessage>(new HttpRequestException("No network"));
}

/// <summary>Builds a plain OpenAI chat completion response.</summary>
internal static class JsonResponseFactory
{
    public static HttpResponseMessage Completion(string content)
    {
        var payload = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { message = new { role = "assistant", content } }
            }
        });

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
    }
}
