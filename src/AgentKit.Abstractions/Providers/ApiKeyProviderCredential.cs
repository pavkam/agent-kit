// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Text;

/// <summary>
/// A provider credential consisting of one static, long-lived API key
/// supplied by the application's configuration.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over
/// <see cref="ApiKey"/>. Equality and hashing over a secret value is
/// intentional here (values are compared, not logged), but callers must
/// still take care never to place an instance where it could be
/// serialized, logged, or otherwise made observable outside the request
/// pipeline that consumes it. <see cref="ToString"/> and
/// <see cref="PrintMembers"/> are overridden so the synthesized textual
/// form never prints <see cref="ApiKey"/>, matching
/// <c>ProviderAuthorizationGranted</c>.
/// </remarks>
public sealed record ApiKeyProviderCredential: ProviderCredential
{
    /// <summary>The fixed text substituted for <see cref="ApiKey"/> in <see cref="ToString"/>.</summary>
    public const string RedactionMarker = "[REDACTED]";

    /// <summary>Initializes a new instance of the <see cref="ApiKeyProviderCredential"/> record.</summary>
    /// <param name="apiKey">The non-empty API key text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="apiKey"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public ApiKeyProviderCredential(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ApiKey = apiKey;
    }

    /// <summary>Gets the API key text.</summary>
    public string ApiKey { get; init; }

    /// <summary>Returns a textual form that redacts <see cref="ApiKey"/>.</summary>
    /// <returns><c>ApiKeyProviderCredential { ApiKey = [REDACTED] }</c>.</returns>
    public override string ToString()
    {
        var builder = new StringBuilder();
        _ = builder.Append(nameof(ApiKeyProviderCredential)).Append(" { ");
        _ = PrintMembers(builder);
        _ = builder.Append(" }");
        return builder.ToString();
    }

    /// <summary>Appends the printable members to <paramref name="builder"/>, writing <see cref="RedactionMarker"/> in place of <see cref="ApiKey"/>.</summary>
    /// <param name="builder">The builder receiving the member text.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    protected override bool PrintMembers(StringBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _ = builder.Append(nameof(ApiKey)).Append(" = ").Append(RedactionMarker);
        return true;
    }
}
