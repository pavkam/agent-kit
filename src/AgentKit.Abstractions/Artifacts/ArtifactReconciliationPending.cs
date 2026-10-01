// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that the artifact was conservatively retained because no terminal disposition could be established yet.</summary>
public sealed record ArtifactReconciliationPending: ArtifactReconciliationResult
{
    /// <summary>Initializes a pending reconciliation outcome.</summary>
    /// <param name="reason">Why the artifact was retained.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reason"/> is undefined.</exception>
    public ArtifactReconciliationPending(ArtifactReconciliationPendingReason reason)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(reason);
        Reason = reason;
    }

    /// <summary>Gets why the artifact was retained.</summary>
    public ArtifactReconciliationPendingReason Reason { get; }
}
