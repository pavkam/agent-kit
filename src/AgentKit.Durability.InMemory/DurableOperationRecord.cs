// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory;

/// <summary>Stores one durable operation's accumulated journal state under the journal's single gate.</summary>
/// <remarks>All access to instances of this type is serialized by <see cref="InMemoryDurableOperationJournal"/>'s single gate; this class performs no synchronization of its own.</remarks>
internal sealed class DurableOperationRecord
{
    /// <summary>Initializes a freshly accepted record.</summary>
    /// <param name="binding">The exact durable coordinates and captured authorization this operation was accepted under.</param>
    /// <param name="fencingToken">The ownership generation that committed acceptance.</param>
    internal DurableOperationRecord(DurableOperationBinding binding, FencingToken fencingToken)
    {
        Binding = binding;
        State = DurableOperationState.Accepted;
        SideEffectCertainty = SideEffectCertainty.DefinitelyNotPerformed;
        LastWriterToken = fencingToken;
    }

    /// <summary>Gets the exact durable coordinates and captured authorization this operation was accepted under.</summary>
    internal DurableOperationBinding Binding { get; }

    /// <summary>Gets or sets the persisted lifecycle position.</summary>
    internal DurableOperationState State { get; set; }

    /// <summary>Gets or sets what is actually known about whether the external effect occurred.</summary>
    internal SideEffectCertainty SideEffectCertainty { get; set; }

    /// <summary>Gets or sets the most recent complete state snapshot, or <see langword="null"/> when none was recorded.</summary>
    internal DurableCheckpoint? LatestCheckpoint { get; set; }

    /// <summary>Gets or sets the retained terminal record, or <see langword="null"/> until <see cref="TerminalResultRecorded"/> is true.</summary>
    internal DurableOperationResult? TerminalResult { get; set; }

    /// <summary>Gets whether a complete terminal result exists and may be committed without reinvoking the effect.</summary>
    internal bool TerminalResultRecorded => TerminalResult is not null;

    /// <summary>Gets or sets the ownership generation of the last successful durable write.</summary>
    internal FencingToken LastWriterToken { get; set; }
}
