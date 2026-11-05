using System.Net;

namespace Rldump.Api;

/// <summary>The <c>Error</c> member of the Prime API response envelope.</summary>
internal sealed class ApiError
{
    public string Message { get; init; } = "";
    public string Type { get; init; } = "";
}

/// <summary>
/// The response envelope shared by every Prime API endpoint: exactly one of
/// <see cref="Error"/> or <see cref="Result"/> is present.
/// </summary>
internal sealed class Envelope<TResult>
{
    public ApiError? Error { get; init; }
    public TResult? Result { get; init; }
}

/// <summary>The API answered 2xx but the envelope carried an <c>Error</c>.</summary>
internal sealed class ApiErrorException : Exception
{
    public string Type { get; }

    public ApiErrorException(string type, string message)
        : base(message)
    {
        Type = type;
    }
}

/// <summary>The API answered with a non-2xx HTTP status.</summary>
internal sealed class ApiHttpException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public ApiHttpException(HttpStatusCode statusCode, string path, string? reasonPhrase, string redactedBody)
        : base(BuildMessage(statusCode, path, reasonPhrase, redactedBody))
    {
        StatusCode = statusCode;
    }

    private static string BuildMessage(HttpStatusCode statusCode, string path, string? reasonPhrase, string body)
    {
        string message = $"HTTP {(int)statusCode} from {path}";
        if (!string.IsNullOrWhiteSpace(reasonPhrase))
            message += $": {reasonPhrase}";

        string trimmed = body.Trim();
        if (trimmed.Length == 0)
            return message;
        if (trimmed.Length > 200)
            trimmed = trimmed[..200] + "…";
        return $"{message}\n{trimmed}";
    }
}
