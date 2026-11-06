namespace Rldump.Api;

/// <summary>
/// GraphQL/AccountQuery looks an account up by display name (3–16 chars) or by
/// Epic account ID (32–34 chars). The result is passed through verbatim (no
/// field needs a transform), so only the request is modeled.
/// </summary>
internal sealed class AccountQueryRequest
{
    public string User { get; init; } = "";
}
