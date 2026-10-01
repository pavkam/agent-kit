// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Merges every registered target provider's capture into one deterministic snapshot.</summary>
public interface IDelegationTargetCatalog
{
    /// <summary>Captures and merges all providers' targets.</summary>
    /// <param name="request">The discovery request.</param>
    /// <param name="cancellationToken">Cancels the capture.</param>
    /// <returns>The merged snapshot in provider registration order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<DelegationTargetCatalogSnapshot> CaptureAsync(DelegationDiscoveryRequest request, CancellationToken cancellationToken = default);
}
