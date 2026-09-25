// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock;

/// <summary>
/// Placeholder <see cref="IProviderCredentialSource"/> registered for Bedrock profile runtime binding.
/// </summary>
/// <remarks>
/// Bedrock chat requests are signed with <see cref="IAwsCredentialSource"/>; this type exists so endpoint
/// profile selection can reference a credential profile without implying bearer-token authentication.
/// </remarks>
internal sealed class AwsBedrockProfileCredentialSource: IProviderCredentialSource
{
    /// <inheritdoc/>
    public ValueTask<ProviderCredential> GetCredentialAsync(ProviderId providerId, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(
            "AWS Bedrock conversational requests authenticate through IAwsCredentialSource and SigV4 signing; " +
            "this credential source exists only for provider profile runtime binding.");
}
