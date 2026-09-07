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
/// before it is used to sign a request, and it is never logged.
/// </remarks>
public sealed record AwsSigV4Credential
{
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
}
