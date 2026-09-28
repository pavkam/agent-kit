// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Observes the bounded action a recovery policy chose for one operation from its durable evidence.</summary>
/// <remarks>
/// The event reports the classification, not its execution. It carries only bounded dimensions — the chosen action,
/// the evidence state, and the observed side-effect certainty — so a sink can be exported as a metric without
/// leaking a payload, external handle, or safe message.
/// </remarks>
public sealed record DurableOperationRecoveryPlanned: DurableExecutionEvent
{
    /// <summary>Initializes a recovery-classification observation.</summary>
    /// <param name="binding">The non-null address and captured execution context of the recovering operation.</param>
    /// <param name="occurredAt">The injected-clock instant at which the policy decided.</param>
    /// <param name="action">The bounded action the policy chose.</param>
    /// <param name="evidenceState">The lifecycle state the loaded evidence reported.</param>
    /// <param name="sideEffectCertainty">The external-effect certainty the loaded evidence reported.</param>
    /// <exception cref="ArgumentNullException"><paramref name="binding"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="action"/>, <paramref name="evidenceState"/>, or <paramref name="sideEffectCertainty"/> is not
    /// a defined member.
    /// </exception>
    public DurableOperationRecoveryPlanned(
        DurableOperationBinding binding,
        DateTimeOffset occurredAt,
        DurableRecoveryAction action,
        DurableOperationState evidenceState,
        SideEffectCertainty sideEffectCertainty)
        : base(binding, occurredAt)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(action);
        ArgumentOutOfRangeException.ThrowIfUndefined(evidenceState);
        ArgumentOutOfRangeException.ThrowIfUndefined(sideEffectCertainty);
        Action = action;
        EvidenceState = evidenceState;
        SideEffectCertainty = sideEffectCertainty;
    }

    /// <summary>Gets the bounded action the policy chose.</summary>
    /// <value>A defined <see cref="DurableRecoveryAction"/> member corresponding to one concrete decision kind.</value>
    public DurableRecoveryAction Action { get; }

    /// <summary>Gets the lifecycle state the evidence reported.</summary>
    /// <value>A defined <see cref="DurableOperationState"/> member.</value>
    public DurableOperationState EvidenceState { get; }

    /// <summary>Gets the external-effect certainty the evidence reported.</summary>
    /// <value>A defined <see cref="SideEffectCertainty"/> member.</value>
    public SideEffectCertainty SideEffectCertainty { get; }
}
