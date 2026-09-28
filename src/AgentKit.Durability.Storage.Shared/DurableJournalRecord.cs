// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Storage;

/// <summary>One newline-delimited append-only transition in a durable journal's record log.</summary>
/// <remarks>
/// Exactly one payload is present for each kind, and replay validates that pairing rather than tolerating a record
/// whose kind and payload disagree. Keeping the transitions rather than only the resulting state is what lets the log
/// be appended to without rewriting, which is the property that makes every acknowledged record survive process loss.
/// </remarks>
/// <param name="Kind">The transition discriminator.</param>
/// <param name="Started">The acceptance record, present exactly for <see cref="DurableJournalRecordKind.Started"/>.</param>
/// <param name="Checkpointed">The progress snapshot, present exactly for <see cref="DurableJournalRecordKind.Checkpointed"/>.</param>
/// <param name="Settled">The terminal record, present exactly for <see cref="DurableJournalRecordKind.Settled"/>.</param>
/// <param name="Waiting">The wait record, present exactly for <see cref="DurableJournalRecordKind.Waiting"/>.</param>
/// <param name="Snapshot">The whole-operation state, present exactly for <see cref="DurableJournalRecordKind.Snapshot"/>.</param>
internal sealed record DurableJournalRecord(
    DurableJournalRecordKind Kind,
    DurableOperationStartDocument? Started,
    DurableCheckpointDocument? Checkpointed,
    DurableOperationResultDocument? Settled,
    DurableOperationWaitingDocument? Waiting,
    DurableOperationProjectionDocument? Snapshot)
{
    /// <summary>Creates the transition describing one operation entering the journal.</summary>
    /// <param name="start">The non-null acceptance declaration that was committed.</param>
    /// <returns>A record carrying the complete acceptance declaration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="start"/> is null.</exception>
    internal static DurableJournalRecord ForStarted(DurableOperationStart start) => new(
        DurableJournalRecordKind.Started,
        DurableOperationStartDocument.FromDomain(start),
        null,
        null,
        null,
        null);

    /// <summary>Creates the transition describing one committed progress snapshot.</summary>
    /// <param name="checkpoint">The non-null checkpoint that was committed.</param>
    /// <returns>A record carrying the complete checkpoint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="checkpoint"/> is null.</exception>
    internal static DurableJournalRecord ForCheckpointed(DurableCheckpoint checkpoint) => new(
        DurableJournalRecordKind.Checkpointed,
        null,
        DurableCheckpointDocument.FromDomain(checkpoint),
        null,
        null,
        null);

    /// <summary>Creates the transition describing one committed settlement.</summary>
    /// <param name="terminal">The non-null terminal record that was committed.</param>
    /// <returns>A record carrying the complete terminal result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="terminal"/> is null.</exception>
    internal static DurableJournalRecord ForSettled(DurableOperationResult terminal)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        return new DurableJournalRecord(
            DurableJournalRecordKind.Settled,
            null,
            null,
            DurableOperationResultDocument.FromDomain(terminal),
            null,
            null);
    }

    /// <summary>Creates the transition describing one committed wait.</summary>
    /// <param name="waiting">The non-null wait record that was committed.</param>
    /// <returns>A record carrying the complete wait declaration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="waiting"/> is null.</exception>
    internal static DurableJournalRecord ForWaiting(DurableOperationWaiting waiting) => new(
        DurableJournalRecordKind.Waiting,
        null,
        null,
        null,
        DurableOperationWaitingDocument.FromDomain(waiting),
        null);

    /// <summary>Creates the compaction record describing one operation's complete accumulated state.</summary>
    /// <param name="projection">The non-null projection replacing this operation's transition history.</param>
    /// <returns>A record carrying the whole-operation snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="projection"/> is null.</exception>
    internal static DurableJournalRecord ForSnapshot(DurableOperationProjection projection) => new(
        DurableJournalRecordKind.Snapshot,
        null,
        null,
        null,
        null,
        DurableOperationProjectionDocument.FromDomain(projection));
}
