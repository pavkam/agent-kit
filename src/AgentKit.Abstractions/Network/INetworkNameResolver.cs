// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Resolves one canonical destination under exact single-use network authority.</summary>
public interface INetworkNameResolver
{
    /// <summary>Gets the enforcement audience that resolution grants must name.</summary>
    public ComponentId SecurityAudience { get; }

    /// <summary>Resolves one bounded destination without opening a connection.</summary>
    /// <param name="request">The exact authorized resolution request.</param>
    /// <param name="cancellationToken">A token used to cancel pending resolution.</param>
    /// <returns>The terminal resolution result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<NetworkResolutionResult> ResolveAsync(
        NetworkResolutionRequest request,
        CancellationToken cancellationToken = default);
}
