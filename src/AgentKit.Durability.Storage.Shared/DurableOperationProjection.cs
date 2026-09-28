// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Storage;

/// <summary>Holds one durable operation's accumulated journal state between writes.</summary>
/// <remarks>
/// <para>
/// The projection is the authoritative answer a store gives a recovering worker. It is deliberately the same shape for
/// every storage family so the lifecycle rules in <see cref="DurableJournalTransition"/> can be written once and
/// exercised identically by the SQLite and JSON conformance runs.
/// </para>
/// <para>
/// This class performs no synchronization. A SQLite store materializes one instance inside an immediate transaction and
/// discards it; a JSON store retains instances under its own single gate. Neither shares an instance across concurrent
/// callers.
/// </para>
/// </remarks>
internal sealed class DurableOperationProjection
{
    /// <summary>Initializes a freshly accepted operation that has performed no effect yet.</summary>
    /// <param name="binding">The non-null coordinates and captured authorization acceptance committed under.</param>
    /// <param name="lastWriterToken">The ownership generation that committed acceptance.</param>
    /// <exception cref="ArgumentNullException"><paramref name="binding"/> is null.</exception>
    internal DurableOperationProjection(DurableOperationBinding binding, FencingToken lastWriterToken)
    {
        ArgumentNullException.ThrowIfNull(binding);
        Binding = binding;
        State = DurableOperationState.Accepted;
        SideEffectCertainty = SideEffectCertainty.DefinitelyNotPerformed;
        LastWriterToken = lastWriterToken;
    }

    /// <summary>Gets the exact coordinates and captured authorization this operation was accepted under.</summary>
    /// <value>The immutable binding a later write must present unchanged; a mismatch is a conflicting-context failure, never an overwrite.</value>
    internal DurableOperationBinding Binding { get; }

    /// <summary>Gets the operation coordinates this projection answers for.</summary>
    /// <value>The address derived from <see cref="Binding"/>.</value>
    internal DurableOperationAddress Address => Binding.Address;

    /// <summary>Gets or sets the declaration acceptance committed, so recovery evidence can carry it back.</summary>
    /// <value>The retained declaration, or <see langword="null"/> before acceptance committed one.</value>
    internal RecoverableOperationDescriptor? Descriptor { get; set; }

    /// <summary>Gets or sets the persisted lifecycle position.</summary>
    /// <value>The state computed from whichever write committed most recently, never a separate caller claim.</value>
    internal DurableOperationState State { get; set; }

    /// <summary>Gets or sets what is provably known about whether the external effect occurred.</summary>
    /// <value>The certainty computed from the most recent committed write.</value>
    internal SideEffectCertainty SideEffectCertainty { get; set; }

    /// <summary>Gets or sets the most recent complete state snapshot.</summary>
    /// <value>The latest checkpoint, or <see langword="null"/> when none was recorded.</value>
    internal DurableCheckpoint? LatestCheckpoint { get; set; }

    /// <summary>Gets or sets the retained terminal record.</summary>
    /// <value>The settling record, or <see langword="null"/> until the operation settles.</value>
    internal DurableOperationResult? TerminalResult { get; set; }

    /// <summary>Gets whether a complete terminal result exists and may be committed without reinvoking the effect.</summary>
    /// <value><see langword="true"/> once a terminal record is retained.</value>
    internal bool TerminalResultRecorded => TerminalResult is not null;

    /// <summary>Gets or sets the deferred wake instant while the operation waits.</summary>
    /// <value>The earliest resumption instant, or <see langword="null"/> when the operation may resume immediately.</value>
    internal DateTimeOffset? NotBefore { get; set; }

    /// <summary>Gets or sets the external owner's handle when work was handed off.</summary>
    /// <value>The handle naming the awaited external work, or <see langword="null"/> when none was issued.</value>
    internal ExternalOperationReference? ExternalReference { get; set; }

    /// <summary>Gets or sets the external idempotency key established while waiting.</summary>
    /// <value>The key the effect owner collapses duplicate attempts on, or <see langword="null"/> when none applies.</value>
    internal IdempotencyKey? ExternalIdempotencyKey { get; set; }

    /// <summary>Gets or sets the ownership generation of the last successful durable write.</summary>
    /// <value>The fence a later writer must meet or exceed to be accepted.</value>
    internal FencingToken LastWriterToken { get; set; }

    /// <summary>Projects the accumulated state into the evidence a recovering worker classifies from.</summary>
    /// <returns>Complete recovery evidence derived entirely from committed writes.</returns>
    /// <remarks>
    /// <see cref="RecoveryEvidence.StartDefinitelyAbsent"/> is derived from the computed state rather than a stored
    /// flag: an operation that only ever committed acceptance provably performed no effect, and any later write moves
    /// it out of that state permanently.
    /// </remarks>
    internal RecoveryEvidence ToEvidence() => new(
        Binding,
        State,
        SideEffectCertainty,
        startDefinitelyAbsent: State == DurableOperationState.Accepted,
        terminalResultRecorded: TerminalResultRecorded,
        recordedResult: TerminalResult,
        notBefore: NotBefore,
        LatestCheckpoint,
        externalReference: ExternalReference,
        externalIdempotencyKey: ExternalIdempotencyKey,
        LastWriterToken,
        Descriptor);
}
