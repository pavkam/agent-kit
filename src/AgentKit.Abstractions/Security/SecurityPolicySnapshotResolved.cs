// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit;
/// <summary>Reports that the exact requested policy snapshot is currently retrievable.</summary>
public sealed record SecurityPolicySnapshotResolved: SecurityPolicySnapshotResult
{
    /// <summary>Initializes a resolved snapshot result.</summary><param name="reference">The non-null resolved snapshot reference.</param><exception cref="ArgumentNullException"><paramref name="reference"/> is null.</exception>
    public SecurityPolicySnapshotResolved(SecurityPolicySnapshotReference reference) { ArgumentNullException.ThrowIfNull(reference); Reference = reference; }
    /// <summary>Gets the resolved snapshot reference.</summary><value>Non-null immutable snapshot identity, version, and fingerprint.</value>
    public SecurityPolicySnapshotReference Reference { get; }
}
