// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Observes that one operation's authoritative terminal record committed.</summary>
/// <remarks>
/// Recording a terminal result is not publication. An operation whose state is
/// <see cref="DurableOperationState.OutcomeReady"/> is settled in the journal but still awaiting its source-ordered
/// projection, so a sink must never treat this event as permission to reinvoke or to publish out of order.
/// </remarks>
public sealed record DurableOperationSettled: DurableExecutionEvent
{
    /// <summary>Initializes a terminal-record observation.</summary>
    /// <param name="binding">The non-null address and captured execution context of the settled operation.</param>
    /// <param name="occurredAt">The injected-clock instant at which the terminal record committed.</param>
    /// <param name="state">The recorded terminal lifecycle state.</param>
    /// <param name="sideEffectCertainty">The truthful certainty about the relevant external effect.</param>
    /// <param name="fencingToken">The ownership generation that committed the terminal record.</param>
    /// <exception cref="ArgumentNullException"><paramref name="binding"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="state"/> or <paramref name="sideEffectCertainty"/> is not a defined member, or
    /// <paramref name="fencingToken"/> is the default value.
    /// </exception>
    public DurableOperationSettled(
        DurableOperationBinding binding,
        DateTimeOffset occurredAt,
        DurableOperationState state,
        SideEffectCertainty sideEffectCertainty,
        FencingToken fencingToken)
        : base(binding, occurredAt)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(state);
        ArgumentOutOfRangeException.ThrowIfUndefined(sideEffectCertainty);
        ArgumentOutOfRangeException.ThrowIfEqual(fencingToken, default, nameof(fencingToken));
        State = state;
        SideEffectCertainty = sideEffectCertainty;
        FencingToken = fencingToken;
    }

    /// <summary>Gets the recorded terminal lifecycle state.</summary>
    /// <value>A defined <see cref="DurableOperationState"/> member describing how the operation ended.</value>
    public DurableOperationState State { get; }

    /// <summary>Gets the truthful external-effect certainty.</summary>
    /// <value>
    /// A defined <see cref="SideEffectCertainty"/> member concerning the relevant external effect, never a claim
    /// about whether the result record itself was written.
    /// </value>
    public SideEffectCertainty SideEffectCertainty { get; }

    /// <summary>Gets the ownership generation that committed the terminal record.</summary>
    /// <value>The non-default fencing token presented by the settling worker.</value>
    public FencingToken FencingToken { get; }
}
