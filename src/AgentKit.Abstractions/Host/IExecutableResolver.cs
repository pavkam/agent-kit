// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Canonicalizes one structured process start request before authorization.</summary>
public interface IExecutableResolver
{
    /// <summary>Gets the sole component audience permitted to consume resolution observation grants.</summary>
    public ComponentId SecurityAudience { get; }

    /// <summary>Reduces one start request to resolved executable and workspace facts.</summary>
    /// <param name="request">The unresolved start request.</param>
    /// <param name="cancellationToken">Cancels resolution work before protected observation.</param>
    /// <returns>A closed resolution outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<ExecutableResolutionResult> ResolveAsync(
        ProcessStartRequest request,
        CancellationToken cancellationToken = default);
}
