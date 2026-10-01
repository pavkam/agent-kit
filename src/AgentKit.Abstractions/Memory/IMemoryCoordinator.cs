// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Validates, authorizes, and delegates durable-memory mutation.</summary>
/// <remarks>
/// <para>
/// Each operation asks the memory-profile runtime selector for the exact profile key and version in its context, holds the
/// lease through policy, authorization, storage, and observation, and disposes it. Policy decides whether a proposal may
/// become durable state; the coordinator never writes without an explicit allow and a single-use grant from the security
/// authority the context's authorization names. A model may propose a memory but cannot authorize retention.
/// </para>
/// <para>Deletion commits an authoritative tombstone before physical purge. Observation events are published after the store committed.</para>
/// </remarks>
public interface IMemoryCoordinator
{
    /// <summary>Evaluates a proposal against policy and, only on an explicit allow, makes it durable.</summary>
    /// <param name="proposal">The proposal.</param>
    /// <param name="cancellationToken">Cancels before the write commits.</param>
    /// <returns>The durable record, a policy denial, or a typed refusal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="proposal"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the write commits.</exception>
    public ValueTask<MemoryProposalResult> ProposeAsync(MemoryProposal proposal, CancellationToken cancellationToken = default);

    /// <summary>Corrects an active memory by appending an allowed replacement and marking the original corrected.</summary>
    /// <param name="request">The correction.</param>
    /// <param name="cancellationToken">Cancels before the write commits.</param>
    /// <returns>The transition result, or a typed refusal; a policy denial is reported as a denied refusal before any write.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the write commits.</exception>
    public ValueTask<MemoryTransitionResult> CorrectAsync(MemoryCorrectionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes a memory with a tombstone and, when requested, a physical purge.</summary>
    /// <param name="command">The deletion.</param>
    /// <param name="cancellationToken">Cancels before the write commits.</param>
    /// <returns>A receipt separating logical invisibility from physical purge, or a typed refusal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the write commits.</exception>
    public ValueTask<MemoryDeleteResult> DeleteAsync(MemoryDeleteCommand command, CancellationToken cancellationToken = default);
}
