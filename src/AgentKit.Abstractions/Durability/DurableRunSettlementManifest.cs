// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The journaled manifest of one run's terminal settlement.</summary>
/// <remarks>
/// <para>
/// Settlement is recorded as a semantic outcome kind and a message count, never as the run's answer. The messages
/// are already durable session truth, and copying model output into a durable operation record would turn a
/// recovery journal into a second, unredacted transcript.
/// </para>
/// <para>
/// This type is an immutable value object with structural equality over its fields. It carries no mutable state and
/// is safe to share across threads without synchronization.
/// </para>
/// </remarks>
public sealed record DurableRunSettlementManifest
{
    /// <summary>Initializes one complete run-settlement manifest.</summary>
    /// <param name="runId">The nonempty identity of the run that settled.</param>
    /// <param name="messageCount">The nonnegative number of messages this run committed.</param>
    /// <param name="outcomeKind">
    /// The nonblank stable name of the run's semantic outcome, such as the outcome type's name. It names the
    /// classification only and carries no safe message, prompt, or model output.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="runId"/> is the empty identity, or <paramref name="messageCount"/> is negative.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="outcomeKind"/> is null, empty, or whitespace.</exception>
    public DurableRunSettlementManifest(Guid runId, int messageCount, string outcomeKind)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(runId, Guid.Empty, nameof(runId));
        ArgumentOutOfRangeException.ThrowIfNegative(messageCount);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcomeKind);
        RunId = runId;
        MessageCount = messageCount;
        OutcomeKind = outcomeKind;
    }

    /// <summary>Gets the run that settled.</summary>
    /// <value>The nonempty run identity, matching the durable address this operation is journaled under.</value>
    public Guid RunId { get; }

    /// <summary>Gets how many messages this run committed.</summary>
    /// <value>A nonnegative count, which lets a replay detect that it is describing a different run of the same identity.</value>
    public int MessageCount { get; }

    /// <summary>Gets the stable name of the run's semantic outcome.</summary>
    /// <value>
    /// A nonblank classification name. Semantic outcome and settlement status stay separate durable facts: this
    /// value describes what the run concluded, not whether its settlement or recovery completed.
    /// </value>
    public string OutcomeKind { get; }
}
