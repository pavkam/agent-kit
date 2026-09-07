// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock;

/// <summary>
/// An <see cref="IAwsCredentialSource"/> that always resolves to one fixed,
/// caller-supplied <see cref="AwsSigV4Credential"/>.
/// </summary>
/// <remarks>
/// This is appropriate for a long-term IAM user access key/secret key pair
/// configured directly by an application. An application whose credentials
/// are temporary (an assumed IAM role, AWS STS, or an EC2/ECS/Lambda
/// instance credential provider) supplies its own
/// <see cref="IAwsCredentialSource"/> that refreshes the resolved
/// <see cref="AwsSigV4Credential"/> before it expires, since this type
/// never refreshes or expires its fixed credential.
/// </remarks>
public sealed class StaticAwsCredentialSource: IAwsCredentialSource
{
    private readonly AwsSigV4Credential _credential;

    /// <summary>Initializes a new instance of the <see cref="StaticAwsCredentialSource"/> class.</summary>
    /// <param name="credential">The credential every call resolves to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="credential"/> is null.</exception>
    public StaticAwsCredentialSource(AwsSigV4Credential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        _credential = credential;
    }

    /// <inheritdoc/>
    public ValueTask<AwsSigV4Credential> GetCredentialAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_credential);
    }
}
