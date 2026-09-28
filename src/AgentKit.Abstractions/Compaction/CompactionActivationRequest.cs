// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One idempotent activation attempt for a validated compaction candidate.</summary>
public sealed record CompactionActivationRequest
{
    /// <summary>Initializes a new instance of the <see cref="CompactionActivationRequest"/> record.</summary>
    /// <param name="request">The compaction request being activated.</param>
    /// <param name="compaction">The validated compaction candidate.</param>
    /// <param name="expectedVersion">The branch version the append must match.</param>
    /// <param name="branchTip">The branch tip observed before activation.</param>
    /// <param name="idempotencyKey">The idempotency key for the append.</param>
    /// <param name="lastCoveredEntryId">The last covered source entry identity.</param>
    /// <param name="supersedes">The prior compaction record this attempt supersedes, if any.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="compaction"/> is null.</exception>
    public CompactionActivationRequest(
        CompactionRequest request,
        ValidatedCompaction compaction,
        SessionVersion expectedVersion,
        SessionSequence branchTip,
        SessionEntryId lastCoveredEntryId,
        IdempotencyKey idempotencyKey,
        CompactionId? supersedes)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(compaction);
        Request = request;
        Compaction = compaction;
        ExpectedVersion = expectedVersion;
        BranchTip = branchTip;
        LastCoveredEntryId = lastCoveredEntryId;
        IdempotencyKey = idempotencyKey;
        Supersedes = supersedes;
    }

    /// <summary>Gets the compaction request being activated.</summary>
    public CompactionRequest Request { get; }

    /// <summary>Gets the validated compaction candidate.</summary>
    public ValidatedCompaction Compaction { get; }

    /// <summary>Gets the branch version the append must match.</summary>
    public SessionVersion ExpectedVersion { get; }

    /// <summary>Gets the branch tip observed before activation.</summary>
    public SessionSequence BranchTip { get; }

    /// <summary>Gets the last covered source entry identity.</summary>
    public SessionEntryId LastCoveredEntryId { get; }

    /// <summary>Gets the idempotency key for the append.</summary>
    public IdempotencyKey IdempotencyKey { get; }

    /// <summary>Gets the prior compaction record this attempt supersedes, if any.</summary>
    public CompactionId? Supersedes { get; }
}
