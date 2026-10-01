// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Discovers delegation targets from one additive source, such as the locally registered agent definitions or a remote worker pool.</summary>
/// <remarks>Each call transfers an independent capture. Providers are additive, never select a target, never infer authority, and are thread-safe.</remarks>
public interface IDelegationTargetProvider
{
    /// <summary>Captures the targets this source currently publishes for one parent.</summary>
    /// <param name="request">The discovery request.</param>
    /// <param name="cancellationToken">Cancels the discovery.</param>
    /// <returns>An independent snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<DelegationTargetSnapshot> DiscoverAsync(DelegationDiscoveryRequest request, CancellationToken cancellationToken = default);
}
