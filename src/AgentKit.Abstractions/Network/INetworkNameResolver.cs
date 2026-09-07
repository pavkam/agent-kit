// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Resolves a network destination to a bounded set of addresses, enforcing a configured destination policy.</summary>
/// <remarks>
/// See <see cref="NetworkDestinationPolicy"/> for the reduced-scope
/// rationale shared by every implementation of this contract: this
/// resolver enforces its own structural policy directly rather than
/// consulting a separate security authority and grant.
/// </remarks>
public interface INetworkNameResolver
{
    /// <summary>Resolves one destination.</summary>
    /// <param name="request">The resolution request.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the terminal resolution outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<NetworkResolutionResult> ResolveAsync(
        NetworkResolutionRequest request, CancellationToken cancellationToken = default);
}
