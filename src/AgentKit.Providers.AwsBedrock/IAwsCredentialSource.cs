// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock;

/// <summary>
/// Resolves the current <see cref="AwsSigV4Credential"/> used to sign
/// Bedrock Runtime requests.
/// </summary>
/// <remarks>
/// This is the AWS-specific analog of AgentKit's shared
/// <see cref="IProviderCredentialSource"/>, not an implementation of it:
/// SigV4 credential material (an access key ID, secret access key, and
/// optional session token) is not expressible as an
/// <see cref="ApiKeyProviderCredential"/> or
/// <see cref="OAuthTokenProviderCredential"/> without either losing
/// structure or inventing a delimited-string encoding, so this package
/// defines its own narrow resolution contract instead.
/// </remarks>
public interface IAwsCredentialSource
{
    /// <summary>Resolves the current AWS credential to sign a request with.</summary>
    /// <param name="cancellationToken">A token used to cancel resolution.</param>
    /// <returns>The resolved AWS credential.</returns>
    public ValueTask<AwsSigV4Credential> GetCredentialAsync(CancellationToken cancellationToken = default);
}
