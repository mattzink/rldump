using System.Net;
using System.Text;

namespace Rldump.Tests.Fakes;

/// <summary>
/// An HttpMessageHandler that returns canned responses and records every
/// request for assertions. No test touches the network.
/// </summary>
public sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

    public sealed record Request(
        string Method,
        Uri Uri,
        string Body,
        string? Authorization,
        string? ContentType);

    public List<Request> Requests { get; } = [];

    public FakeHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        => _responder = responder;

    public static FakeHttpHandler JsonResponse(string json) =>
        new((_, _) => Task.FromResult(Ok(json)));

    public static FakeHttpHandler Status(HttpStatusCode status, string body) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        }));

    public static FakeHttpHandler Throw(Exception exception) =>
        new((_, _) => Task.FromException<HttpResponseMessage>(exception));

    private static HttpResponseMessage Ok(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string body = request.Content is null
            ? ""
            : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new Request(
            request.Method.Method,
            request.RequestUri!,
            body,
            request.Headers.Authorization?.ToString(),
            request.Content?.Headers.ContentType?.ToString()));
        return await _responder(request, cancellationToken);
    }
}
