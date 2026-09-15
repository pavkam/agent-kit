// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

/// <summary>
/// A non-message <see cref="SessionEntry"/> standing in for a fact a tool commits directly to the session
/// mid-turn (the plan/todo tool, for one). It lets tests distinguish a concurrent writer that only records
/// operational state from one that interleaves a conversation message.
/// </summary>
internal sealed record FakeToolFactSessionEntry: SessionEntry
{
    /// <summary>Initializes a fact entry at one branch position.</summary>
    /// <param name="id">The stable entry identity.</param>
    /// <param name="address">The owning session.</param>
    /// <param name="correlation">The operation that recorded the fact.</param>
    /// <param name="branchId">The branch the fact belongs to.</param>
    /// <param name="sequence">The whole-session sequence of the fact.</param>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> or <paramref name="correlation"/> is null.</exception>
    public FakeToolFactSessionEntry(
        SessionEntryId id,
        SessionAddress address,
        OperationCorrelation correlation,
        BranchId branchId,
        SessionSequence sequence)
        : base(id, address, correlation, branchId, sequence, causalParentId: null, DateTimeOffset.UnixEpoch, new SchemaVersion("1"))
    {
    }
}
