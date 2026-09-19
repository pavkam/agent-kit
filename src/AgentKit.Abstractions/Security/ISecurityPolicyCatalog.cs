// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Resolves a captured policy-snapshot reference against the engine's retained effective-policy publications.</summary>
/// <remarks>A catalog retains a snapshot for at least the maximum grant/approval lifetime. It never falls back to the latest publication when the exact requested reference cannot be resolved.</remarks>
public interface ISecurityPolicyCatalog
{
    /// <summary>Resolves one exact policy-snapshot reference.</summary>
    /// <param name="reference">The captured reference to resolve.</param>
    /// <param name="cancellationToken">Cancels resolution before it completes.</param>
    /// <returns>The terminal resolution result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is null.</exception>
    public ValueTask<SecurityPolicySnapshotResult> ResolveAsync(
        SecurityPolicySnapshotReference reference,
        CancellationToken cancellationToken = default);
}
