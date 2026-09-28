// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a durability runtime lease was activated for one attempt.</summary>
public sealed record DurabilityRuntimeActivated: DurabilityRuntimeActivationResult
{
    /// <summary>Initializes a successful activation.</summary>
    /// <param name="lease">The attempt-scoped runtime lease.</param>
    /// <exception cref="ArgumentNullException"><paramref name="lease"/> is null.</exception>
    public DurabilityRuntimeActivated(IDurabilityRuntimeLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        Lease = lease;
    }

    /// <summary>Gets the activated runtime lease.</summary>
    public IDurabilityRuntimeLease Lease { get; }
}
