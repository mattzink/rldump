using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Rldump.Api;

/// <summary>
/// Minimal client for the Rocket League Prime API. Every endpoint uses the
/// custom QUERY HTTP verb (the .NET 10 built-in <see cref="HttpMethod.Query"/>,
/// never "POST") plus bearer-token auth from the config file.
/// </summary>
internal sealed class RocketPlanetClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _token;

    public RocketPlanetClient(string baseUrl, string token, TimeSpan timeout, HttpMessageHandler? handler = null)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("token must be non-empty", nameof(token));

        _baseUrl = baseUrl.TrimEnd('/');
        _token = token;
        _httpClient = handler is null
            ? new HttpClient { Timeout = timeout }
            : new HttpClient(handler, disposeHandler: false) { Timeout = timeout };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>Runs a QUERY request and returns the <c>Result</c> payload of the envelope.</summary>
    public async Task<T> QueryAsync<TRequest, T>(
        string path,
        TRequest request,
        JsonTypeInfo<TRequest> requestType,
        JsonTypeInfo<Envelope<T>> responseType,
        CancellationToken ct = default)
        where T : class
    {
        string body = await QueryRawAsync(path, request, requestType, ct).ConfigureAwait(false);
        Envelope<T> envelope = JsonSerializer.Deserialize(body, responseType)
            ?? throw new ApiErrorException("MalformedResponse", "the response body was empty");
        if (envelope.Error is not null)
            throw new ApiErrorException(envelope.Error.Type, Redact(envelope.Error.Message));
        return envelope.Result
            ?? throw new ApiErrorException("MalformedResponse", "the response contained neither Result nor Error");
    }

    /// <summary>
    /// Runs a QUERY request and returns the opaque <c>Result</c> payload, for
    /// endpoints whose result has no reflected shape (e.g. GetScheduleRegions).
    /// </summary>
    public async Task<JsonElement> QueryOpaqueAsync<TRequest>(
        string path,
        TRequest request,
        JsonTypeInfo<TRequest> requestType,
        JsonTypeInfo<Envelope<JsonElement>> responseType,
        CancellationToken ct = default)
    {
        string body = await QueryRawAsync(path, request, requestType, ct).ConfigureAwait(false);
        Envelope<JsonElement> envelope = JsonSerializer.Deserialize(body, responseType)
            ?? throw new ApiErrorException("MalformedResponse", "the response body was empty");
        if (envelope.Error is not null)
            throw new ApiErrorException(envelope.Error.Type, Redact(envelope.Error.Message));
        if (envelope.Result.ValueKind == System.Text.Json.JsonValueKind.Undefined)
            throw new ApiErrorException("MalformedResponse", "the response contained neither Result nor Error");
        return envelope.Result;
    }

    /// <summary>Runs a QUERY request and returns the raw response body, unmodified.</summary>
    public async Task<string> QueryRawAsync<TRequest>(
        string path,
        TRequest request,
        JsonTypeInfo<TRequest> requestType,
        CancellationToken ct = default)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(request, requestType);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Query, new Uri(_baseUrl + path))
        {
            Content = new ByteArrayContent(body),
        };
        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, ct).ConfigureAwait(false);
        string responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new ApiHttpException(response.StatusCode, path, response.ReasonPhrase, Redact(responseBody));
        return responseBody;
    }

    /// <summary>Defensively removes the bearer token from text that will surface in errors.</summary>
    public string Redact(string text) =>
        text.Contains(_token, StringComparison.Ordinal)
            ? text.Replace(_token, "[redacted]", StringComparison.Ordinal)
            : text;
}
