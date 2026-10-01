// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using AgentKit.Providers.Credentials;

/// <summary>
/// An <see cref="IProviderCredentialSource"/> test double that releases a lease over one fixed
/// <see cref="ProviderCredential"/> without validating the credential-read grant, so adapter tests can exercise header
/// application without composing a grant store. Grant enforcement has its own tests against the first-party sources.
/// </summary>
public sealed class StaticProviderCredentialSource: IProviderCredentialSource
{
    private readonly ProviderCredential _credential;

    /// <summary>Initializes a new instance of the <see cref="StaticProviderCredentialSource"/> class.</summary>
    /// <param name="credential">The credential every resolution leases.</param>
    /// <exception cref="ArgumentNullException"><paramref name="credential"/> is null.</exception>
    public StaticProviderCredentialSource(ProviderCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        _credential = credential;
    }

    /// <inheritdoc/>
    public ProviderCredentialSourceKey Key => StaticProviderProfileRuntimeSelector.SourceKey;

    /// <inheritdoc/>
    public ComponentId SecurityAudience => ProviderCredentialReadGate.DefaultAudience;

    /// <summary>Gets how many times a credential lease was requested.</summary>
    public int ResolutionCount { get; private set; }

    /// <inheritdoc/>
    public ValueTask<ProviderCredentialResolutionResult> ResolveAsync(
        ProviderCredentialResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ResolutionCount++;
        return ValueTask.FromResult<ProviderCredentialResolutionResult>(
            new ProviderCredentialResolved(new ProviderCredentialLease(_credential)));
    }
}
