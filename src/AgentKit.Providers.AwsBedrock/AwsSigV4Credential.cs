// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock;

/// <summary>
/// The AWS credential material needed to sign one Bedrock Runtime request
/// with AWS Signature Version 4.
/// </summary>
/// <remarks>
/// This type deliberately does not extend AgentKit's shared
/// <see cref="ProviderCredential"/> hierarchy. That hierarchy's constructor
/// is <see langword="private protected"/> specifically so that a provider
/// package needing a materially different authentication shape defines its
/// own credential type instead of forcing an AWS access key/secret
/// key/session token triple into a single opaque
/// <see cref="ApiKeyProviderCredential.ApiKey"/> string or an
/// <see cref="OAuthTokenProviderCredential"/> bearer token, neither of
/// which represents SigV4's per-request signing material honestly. A
/// credential value is never stored in a <see cref="ModelDescriptor"/>,
/// message, configuration snapshot, event, exception, or replay log; it is
/// resolved fresh from an <see cref="IAwsCredentialSource"/> immediately
/// before it is used to sign a request, and it is never logged. To make
/// that hold even for an accidental <see cref="object.ToString"/>, this
/// record overrides <see cref="ToString"/> and <see cref="PrintMembers"/>
/// so <see cref="SecretAccessKey"/> and <see cref="SessionToken"/> render
/// as the fixed <see cref="RedactionMarker"/>; only the non-secret
/// <see cref="AccessKeyId"/> is printed verbatim. Structural equality and
/// hashing remain over all three fields, matching the shared
/// <see cref="ProviderCredential"/> kinds: values are compared, not
/// rendered.
/// </remarks>
public sealed record AwsSigV4Credential
{
    /// <summary>
    /// The fixed text substituted for <see cref="SecretAccessKey"/> and a
    /// non-null <see cref="SessionToken"/> in <see cref="ToString"/> and
    /// <see cref="PrintMembers"/>.
    /// </summary>
    public const string RedactionMarker = "[REDACTED]";

    /// <summary>Initializes a new instance of the <see cref="AwsSigV4Credential"/> record.</summary>
    /// <param name="accessKeyId">The AWS access key ID.</param>
    /// <param name="secretAccessKey">The AWS secret access key.</param>
    /// <param name="sessionToken">
    /// The AWS session token, when the credential is a temporary security
    /// credential (for example, from an IAM role or AWS STS), or
    /// <see langword="null"/> for a long-term IAM user credential.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="accessKeyId"/> or <paramref name="secretAccessKey"/>
    /// is null, empty, or consists only of whitespace.
    /// </exception>
    public AwsSigV4Credential(string accessKeyId, string secretAccessKey, string? sessionToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessKeyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretAccessKey);

        AccessKeyId = accessKeyId;
        SecretAccessKey = secretAccessKey;
        SessionToken = sessionToken;
    }

    /// <summary>Gets the AWS access key ID.</summary>
    public string AccessKeyId { get; init; }

    /// <summary>Gets the AWS secret access key.</summary>
    public string SecretAccessKey { get; init; }

    /// <summary>
    /// Gets the AWS session token, when the credential is a temporary
    /// security credential, or <see langword="null"/> for a long-term IAM
    /// user credential.
    /// </summary>
    public string? SessionToken { get; init; }

    /// <summary>
    /// Returns a textual form that shows <see cref="AccessKeyId"/> but
    /// redacts the secret fields.
    /// </summary>
    /// <returns>
    /// <c>AwsSigV4Credential { AccessKeyId = &lt;id&gt;, SecretAccessKey = [REDACTED], SessionToken = [REDACTED] }</c>,
    /// or <c>SessionToken = null</c> for a long-term credential. Neither
    /// secret ever appears in the result.
    /// </returns>
    public override string ToString()
    {
        var builder = new StringBuilder();
        _ = builder.Append(nameof(AwsSigV4Credential)).Append(" { ");
        _ = PrintMembers(builder);
        _ = builder.Append(" }");
        return builder.ToString();
    }

    /// <summary>
    /// Appends the printable members to <paramref name="builder"/>, writing
    /// <see cref="AccessKeyId"/> verbatim, <see cref="RedactionMarker"/> in
    /// place of <see cref="SecretAccessKey"/>, and either
    /// <see cref="RedactionMarker"/> or <c>null</c> for
    /// <see cref="SessionToken"/> depending on whether one is present.
    /// </summary>
    /// <param name="builder">The builder receiving the member text.</param>
    /// <returns>Always <see langword="true"/>, because at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        Debug.Assert(builder is not null, "ToString supplies a fresh builder.");

        _ = builder.Append(nameof(AccessKeyId)).Append(" = ").Append(AccessKeyId);
        _ = builder.Append(", ").Append(nameof(SecretAccessKey)).Append(" = ").Append(RedactionMarker);
        _ = builder.Append(", ").Append(nameof(SessionToken)).Append(" = ").Append(SessionToken is null ? "null" : RedactionMarker);
        return true;
    }
}
