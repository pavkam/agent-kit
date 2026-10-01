// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock;

/// <summary>
/// The application-supplied supplier of the current <see cref="AwsSigV4Credential"/> used to sign Bedrock Runtime
/// requests.
/// </summary>
/// <remarks>
/// This is the AWS analog of <see cref="IOAuthAccessTokenProvider"/>: a raw supplier the application owns, not an
/// <see cref="IProviderCredentialSource"/>. It is never called directly by an adapter. <see cref="AwsSigV4CredentialSource"/>
/// calls it only after validating and consuming the credential-read grant, and wraps the result in an
/// <see cref="AwsSigV4CredentialLease"/>. SigV4 material (an access key ID, secret access key, and optional session
/// token) is not expressible as an <see cref="ApiKeyProviderCredential"/> or <see cref="OAuthTokenProviderCredential"/>
/// without losing structure, so this package defines its own narrow supplier contract. Implementations must be safe to
/// call concurrently and must never log or serialize the credential.
/// </remarks>
public interface IAwsCredentialSource
{
    /// <summary>Resolves the current AWS credential to sign a request with.</summary>
    /// <param name="cancellationToken">A token used to cancel resolution.</param>
    /// <returns>The resolved AWS credential.</returns>
    public ValueTask<AwsSigV4Credential> GetCredentialAsync(CancellationToken cancellationToken = default);
}
