// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Is the persisted payload of a <see cref="GoalTransitionSessionEntry"/>.</summary>
/// <param name="Envelope">The common entry fields.</param>
/// <param name="Transition">The applied transition.</param>
/// <param name="Attempt">The atomic attempt mutation, or <see langword="null"/>.</param>
/// <param name="SettledSequence">The assigned settlement sequence, or <see langword="null"/>.</param>
internal sealed record GoalTransitionEntryDocument(
    GoalEntryEnvelopeDocument Envelope,
    GoalTransitionDocument Transition,
    GoalAttemptChangeDocument? Attempt,
    long? SettledSequence)
{
    /// <summary>Captures an entry.</summary>
    /// <param name="entry">The non-null entry.</param>
    /// <returns>The document.</returns>
    internal static GoalTransitionEntryDocument From(GoalTransitionSessionEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new(
            GoalEntryEnvelopeDocument.From(entry),
            GoalTransitionDocument.FromDomain(entry.Transition),
            entry.Attempt is null ? null : GoalAttemptChangeDocument.FromDomain(entry.Attempt),
            entry.SettledSequence);
    }

    /// <summary>Restores the entry, re-running every domain validation.</summary>
    /// <returns>The entry.</returns>
    internal GoalTransitionSessionEntry ToEntry()
    {
        ArgumentNullException.ThrowIfNull(Envelope);
        ArgumentNullException.ThrowIfNull(Transition);
        return new(
            new SessionEntryId(Envelope.Id),
            Envelope.ToAddress(),
            Envelope.ToCorrelation(),
            new BranchId(Envelope.BranchId),
            new SessionSequence(Envelope.Sequence),
            Envelope.CausalParentId is { } parent ? new SessionEntryId(parent) : null,
            Envelope.RecordedAt,
            new SchemaVersion(Envelope.SchemaVersion),
            Transition.ToDomain(),
            Attempt?.ToDomain(),
            SettledSequence);
    }
}
