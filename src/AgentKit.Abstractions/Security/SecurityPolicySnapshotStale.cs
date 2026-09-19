// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit;
/// <summary>Reports that the requested snapshot reference no longer names the catalog's retained content.</summary>
public sealed record SecurityPolicySnapshotStale: SecurityPolicySnapshotResult
{
    /// <summary>Initializes a stale snapshot result.</summary><param name="reference">The non-null requested reference that could not be honored.</param><param name="safeReason">The nonblank caller-safe reason.</param><exception cref="ArgumentNullException"><paramref name="reference"/> is null.</exception><exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public SecurityPolicySnapshotStale(SecurityPolicySnapshotReference reference, string safeReason)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        Reference = reference;
        SafeReason = safeReason;
    }
    /// <summary>Gets the requested reference that could not be honored.</summary>
    public SecurityPolicySnapshotReference Reference { get; }
    /// <summary>Gets the caller-safe stale reason.</summary>
    public string SafeReason { get; }
}
