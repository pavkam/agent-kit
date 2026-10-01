// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>Is the outcome of a memory proposal: the durable record, a policy denial, or a typed refusal.</summary>
public sealed record MemoryProposalResult
{
    private MemoryProposalResult(MemoryProposalOutcome outcome, DurableMemoryRecord? record, MemoryPolicyDenied? denial, MemoryStoreFailure? failure, bool replayed)
    {
        Outcome = outcome;
        Record = record;
        Denial = denial;
        Failure = failure;
        Replayed = replayed;
    }

    /// <summary>Gets how the proposal ended.</summary>
    public MemoryProposalOutcome Outcome { get; }

    /// <summary>Gets the durable record, or <see langword="null"/> unless the proposal was accepted.</summary>
    public DurableMemoryRecord? Record { get; }

    /// <summary>Gets the policy denial, or <see langword="null"/> unless policy refused the proposal.</summary>
    public MemoryPolicyDenied? Denial { get; }

    /// <summary>Gets the typed refusal, or <see langword="null"/> unless the proposal was rejected.</summary>
    public MemoryStoreFailure? Failure { get; }

    /// <summary>Gets a value indicating whether an earlier equivalent proposal had already created the record.</summary>
    public bool Replayed { get; }

    /// <summary>Gets a value indicating whether the proposal became durable memory.</summary>
    [MemberNotNullWhen(true, nameof(Record))]
    public bool IsAccepted => Outcome == MemoryProposalOutcome.Accepted;

    /// <summary>Creates an accepted result.</summary>
    /// <param name="record">The durable record.</param>
    /// <param name="replayed">Whether the record came from an idempotent replay.</param>
    /// <returns>An accepted result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is null.</exception>
    public static MemoryProposalResult Accepted(DurableMemoryRecord record, bool replayed)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new(MemoryProposalOutcome.Accepted, record, null, null, replayed);
    }

    /// <summary>Creates a policy-denied result.</summary>
    /// <param name="denial">The policy denial.</param>
    /// <returns>A denied result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="denial"/> is null.</exception>
    public static MemoryProposalResult PolicyDenied(MemoryPolicyDenied denial)
    {
        ArgumentNullException.ThrowIfNull(denial);
        return new(MemoryProposalOutcome.PolicyDenied, null, denial, null, false);
    }

    /// <summary>Creates a rejected result.</summary>
    /// <param name="failure">The typed refusal.</param>
    /// <returns>A rejected result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public static MemoryProposalResult Rejected(MemoryStoreFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new(MemoryProposalOutcome.Rejected, null, null, failure, false);
    }
}
