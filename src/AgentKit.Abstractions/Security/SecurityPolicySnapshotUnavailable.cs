// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit;
/// <summary>Reports that snapshot resolution or selection could not complete without a fallback.</summary>
public sealed record SecurityPolicySnapshotUnavailable: SecurityPolicySnapshotResult
{
    /// <summary>Initializes an unavailable snapshot result.</summary><param name="safeReason">The nonblank caller-safe reason.</param><exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public SecurityPolicySnapshotUnavailable(string safeReason) { ArgumentException.ThrowIfNullOrWhiteSpace(safeReason); SafeReason = safeReason; }
    /// <summary>Gets the caller-safe unavailable reason.</summary>
    public string SafeReason { get; }
}
