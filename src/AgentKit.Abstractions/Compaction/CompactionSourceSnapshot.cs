// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A bounded, immutable snapshot of the session entries eligible for one
/// compaction attempt, loaded once through session contracts after
/// authorization.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. A cut selector receives this data directly; it
/// never receives a session store or service provider.
/// </remarks>
public sealed record CompactionSourceSnapshot
{
    /// <summary>Initializes a new instance of the <see cref="CompactionSourceSnapshot"/> record.</summary>
    /// <param name="context">The operation context this snapshot was loaded for.</param>
    /// <param name="branchId">The branch this snapshot was read from.</param>
    /// <param name="version">The branch version this snapshot reflects.</param>
    /// <param name="throughSequence">The last sequence included in this snapshot.</param>
    /// <param name="entries">The eligible entries, in ascending sequence order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="entries"/> is a default, uninitialized array.
    /// </exception>
    public CompactionSourceSnapshot(
        CompactionOperationContext context,
        BranchId branchId,
        SessionVersion version,
        SessionSequence throughSequence,
        ImmutableArray<SessionEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfDefault(entries);

        Context = context;
        BranchId = branchId;
        Version = version;
        ThroughSequence = throughSequence;
        Entries = entries;
    }

    /// <summary>Gets the operation context this snapshot was loaded for.</summary>
    public CompactionOperationContext Context { get; init; }

    /// <summary>Gets the branch this snapshot was read from.</summary>
    public BranchId BranchId { get; init; }

    /// <summary>Gets the branch version this snapshot reflects.</summary>
    public SessionVersion Version { get; init; }

    /// <summary>Gets the last sequence included in this snapshot.</summary>
    public SessionSequence ThroughSequence { get; init; }

    /// <summary>Gets the eligible entries, in ascending sequence order.</summary>
    public ImmutableArray<SessionEntry> Entries { get; init; }
}
