// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Selects the effective policy-snapshot reference that must evaluate one normalized security request.</summary>
public interface ISecurityPolicySelector
{
    /// <summary>Selects the snapshot reference for a request.</summary>
    /// <param name="request">The complete normalized request.</param>
    /// <param name="cancellationToken">Cancels selection before it completes.</param>
    /// <returns>The terminal selection result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<SecurityPolicySnapshotResult> SelectAsync(
        SecurityRequest request,
        CancellationToken cancellationToken = default);
}
