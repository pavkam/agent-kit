// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

/// <summary>
/// The projected state of one session branch: its committed entries and the
/// outcomes of previously accepted idempotent appends.
/// </summary>
/// <remarks>
/// This is a projection of the durable record log, rebuilt identically by replay. All access is serialized by
/// <see cref="JsonSessionStore"/>'s single store-wide gate; this class performs no synchronization of its own.
/// </remarks>
internal sealed class BranchRecord
{
    /// <summary>
    /// Gets the branch's committed entries in append order. This list's
    /// count is always the branch tip's <see cref="SessionSequence"/>, and
    /// each entry's 1-indexed sequence is its position plus one. This count
    /// is independent of any sibling branch's own entry count or sequence
    /// coordinates.
    /// </summary>
    public List<SessionEntry> Entries { get; } = [];

    /// <summary>
    /// Gets the cache of previously accepted append results, keyed by
    /// idempotency key, so a retried append never duplicates entries.
    /// </summary>
    public Dictionary<IdempotencyKey, IdempotencyReceipt<SessionAppendRequest, SessionAppended>> AppendIdempotency { get; } = [];
}
