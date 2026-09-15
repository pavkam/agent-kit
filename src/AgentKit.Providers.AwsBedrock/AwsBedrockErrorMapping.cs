// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock;

using System.Collections.Frozen;
using System.Net;

using AgentKit.Providers.Http;

/// <summary>
/// Maps a Bedrock Runtime exception name or HTTP status code onto the
/// normalized <see cref="ProviderFailureKind"/> taxonomy.
/// </summary>
/// <remarks>
/// Bedrock's buffered <c>Converse</c> errors follow the standard AWS
/// <c>restJson1</c> protocol convention: the exception shape name is
/// carried on the <c>x-amzn-errortype</c> response header (optionally
/// suffixed with a colon and a documentation URL, which callers must
/// strip), while the JSON body carries only a human-readable
/// <c>message</c>. Streaming <c>ConverseStream</c> exception frames carry
/// their exception name directly as the frame's <c>:exception-type</c>
/// reserved header in a distinct, camelCase-first-letter-lowercase
/// vocabulary (for example <c>throttlingException</c> rather than
/// <c>ThrottlingException</c>). <see cref="MapExceptionName(string?)"/>
/// compares case-insensitively so either form maps correctly.
/// </remarks>
internal static class AwsBedrockErrorMapping
{
    /// <summary>
    /// Bedrock-specific HTTP status semantics layered over the shared
    /// <see cref="HttpStatusFailureKindMapper"/> table.
    /// </summary>
    /// <remarks>
    /// Bedrock Runtime returns <c>424 Failed Dependency</c> for
    /// <c>ModelErrorException</c>: the request was valid but the hosted
    /// model itself failed while processing it. That is a transient
    /// provider-side fault, so it is classified as
    /// <see cref="ProviderFailureKind.Unavailable"/> to match the
    /// exception-name mapping rather than the generic 4xx caller-fault
    /// default.
    /// </remarks>
    private static readonly FrozenDictionary<HttpStatusCode, ProviderFailureKind> _statusOverrides =
        new Dictionary<HttpStatusCode, ProviderFailureKind>
        {
            [HttpStatusCode.FailedDependency] = ProviderFailureKind.Unavailable,
        }.ToFrozenDictionary();

    /// <summary>Maps a Bedrock exception shape name onto a normalized failure kind.</summary>
    /// <param name="exceptionName">
    /// The exception shape name, from either the buffered response's
    /// <c>x-amzn-errortype</c> header or a streaming exception frame's
    /// <c>:exception-type</c> header, or <see langword="null"/> if none was
    /// available.
    /// </param>
    /// <returns>
    /// The normalized failure kind, or <see cref="ProviderFailureKind.Unknown"/>
    /// if <paramref name="exceptionName"/> is <see langword="null"/> or not
    /// recognized.
    /// </returns>
    public static ProviderFailureKind MapExceptionName(string? exceptionName)
    {
        if (string.IsNullOrEmpty(exceptionName))
        {
            return ProviderFailureKind.Unknown;
        }

        // The header form may carry a trailing ":<documentation-url>" suffix
        // per the AWS restJson1 protocol convention; only the shape name
        // before the first colon is significant.
        var colonIndex = exceptionName.IndexOf(':');
        var name = colonIndex >= 0 ? exceptionName[..colonIndex] : exceptionName;

        return name.ToUpperInvariant() switch
        {
            "VALIDATIONEXCEPTION" => ProviderFailureKind.InvalidRequest,
            "ACCESSDENIEDEXCEPTION" => ProviderFailureKind.Authorization,
            "EXPIREDTOKENEXCEPTION" => ProviderFailureKind.Authentication,
            "UNRECOGNIZEDCLIENTEXCEPTION" => ProviderFailureKind.Authentication,
            "INCOMPLETESIGNATURE" => ProviderFailureKind.Authentication,
            "NOTAUTHORIZED" => ProviderFailureKind.Authentication,
            "RESOURCENOTFOUNDEXCEPTION" => ProviderFailureKind.InvalidRequest,
            "MODELTIMEOUTEXCEPTION" => ProviderFailureKind.Timeout,
            "REQUESTTIMEOUTEXCEPTION" => ProviderFailureKind.Timeout,
            "MODELERROREXCEPTION" => ProviderFailureKind.Unavailable,
            "MODELSTREAMERROREXCEPTION" => ProviderFailureKind.Unavailable,
            "MODELNOTREADYEXCEPTION" => ProviderFailureKind.Throttling,
            "THROTTLINGEXCEPTION" => ProviderFailureKind.Throttling,
            "SERVICEQUOTAEXCEEDEDEXCEPTION" => ProviderFailureKind.InvalidRequest,
            "REQUESTENTITYTOOLARGEEXCEPTION" => ProviderFailureKind.InvalidRequest,
            "INTERNALSERVEREXCEPTION" => ProviderFailureKind.Unavailable,
            "INTERNALFAILURE" => ProviderFailureKind.Unavailable,
            "SERVICEUNAVAILABLEEXCEPTION" => ProviderFailureKind.Unavailable,
            "SERVICEUNAVAILABLE" => ProviderFailureKind.Unavailable,
            _ => ProviderFailureKind.Unknown,
        };
    }

    /// <summary>
    /// Maps an HTTP status code onto a normalized failure kind, used when
    /// no <c>x-amzn-errortype</c> header is available.
    /// </summary>
    /// <param name="statusCode">The HTTP status code the provider returned.</param>
    /// <returns>
    /// The Bedrock-specific override for <paramref name="statusCode"/> when
    /// one exists; otherwise the shared canonical mapping from
    /// <see cref="HttpStatusFailureKindMapper"/>.
    /// </returns>
    public static ProviderFailureKind MapStatusCode(HttpStatusCode statusCode) =>
        HttpStatusFailureKindMapper.Map(statusCode, _statusOverrides);
}
