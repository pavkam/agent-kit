// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory;

/// <summary>
/// The mutable in-process state of one session branch: its committed
/// entries and the outcomes of previously accepted idempotent appends.
/// </summary>
/// <remarks>
/// All access to instances of this type is serialized by
/// <see cref="InMemorySessionStore"/>'s single store-wide gate; this class
/// performs no synchronization of its own.
/// </remarks>
internal sealed class BranchRecord
{
    /// <summary>
    /// Gets the branch's committed entries in append order. The branch's
    /// <see cref="SessionVersion"/> is always this list's count, and each
    /// entry's 1-indexed <see cref="SessionSequence"/> is its position plus
    /// one.
    /// </summary>
    public List<SessionEntry> Entries { get; } = [];

    /// <summary>
    /// Gets the cache of previously accepted append results, keyed by
    /// idempotency key, so a retried append never duplicates entries.
    /// </summary>
    public Dictionary<IdempotencyKey, IdempotencyReceipt<SessionAppendRequest, SessionAppended>> AppendIdempotency { get; } = [];
}
