// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Is the persisted payload of a <see cref="GoalCreatedSessionEntry"/>.</summary>
/// <param name="Envelope">The common entry fields.</param>
/// <param name="Record">The goal as created.</param>
/// <param name="CreateKey">The creation idempotency key.</param>
internal sealed record GoalCreatedEntryDocument(GoalEntryEnvelopeDocument Envelope, GoalRecordDocument Record, string CreateKey)
{
    /// <summary>Captures an entry.</summary>
    /// <param name="entry">The non-null entry.</param>
    /// <returns>The document.</returns>
    internal static GoalCreatedEntryDocument From(GoalCreatedSessionEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new(GoalEntryEnvelopeDocument.From(entry), GoalRecordDocument.FromDomain(entry.Record), entry.CreateKey.Value);
    }

    /// <summary>Restores the entry, re-running every domain validation.</summary>
    /// <returns>The entry.</returns>
    internal GoalCreatedSessionEntry ToEntry()
    {
        ArgumentNullException.ThrowIfNull(Envelope);
        ArgumentNullException.ThrowIfNull(Record);
        return new(
            new SessionEntryId(Envelope.Id),
            Envelope.ToAddress(),
            Envelope.ToCorrelation(),
            new BranchId(Envelope.BranchId),
            new SessionSequence(Envelope.Sequence),
            Envelope.CausalParentId is { } parent ? new SessionEntryId(parent) : null,
            Envelope.RecordedAt,
            new SchemaVersion(Envelope.SchemaVersion),
            Record.ToDomain(delegation: null),
            new IdempotencyKey(CreateKey));
    }
}
