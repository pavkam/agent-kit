// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using AgentKit.Providers.Credentials;

/// <summary>
/// An <see cref="IProviderCredentialSource"/> test double that always throws a scripted exception, simulating a
/// user-supplied source (for example an Entra token provider) failing during resolution.
/// </summary>
public sealed class ThrowingProviderCredentialSource: IProviderCredentialSource
{
    private readonly Exception _exception;

    /// <summary>Initializes a new instance of the <see cref="ThrowingProviderCredentialSource"/> class.</summary>
    /// <param name="exception">The exception every resolution throws.</param>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is null.</exception>
    public ThrowingProviderCredentialSource(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _exception = exception;
    }

    /// <inheritdoc/>
    public ProviderCredentialSourceKey Key => StaticProviderProfileRuntimeSelector.SourceKey;

    /// <inheritdoc/>
    public ComponentId SecurityAudience => ProviderCredentialReadGate.DefaultAudience;

    /// <inheritdoc/>
    public ValueTask<ProviderCredentialResolutionResult> ResolveAsync(
        ProviderCredentialResolutionRequest request,
        CancellationToken cancellationToken = default) =>
        throw _exception;
}
