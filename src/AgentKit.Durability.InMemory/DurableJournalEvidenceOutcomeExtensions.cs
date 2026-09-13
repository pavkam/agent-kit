// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory;

/// <summary>Maps finite durable-journal evidence-load outcomes to stable diagnostic values.</summary>
internal static class DurableJournalEvidenceOutcomeExtensions
{
    extension(DurableJournalEvidenceOutcome outcome)
    {
        /// <summary>Returns the bounded lowercase value shared by evidence-load logs, activities, and metrics.</summary>
        /// <returns>A stable lowercase terminal-outcome value.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined.</exception>
        internal string ToStableValue()
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(outcome);
            return outcome switch
            {
                DurableJournalEvidenceOutcome.Loaded => "loaded",
                DurableJournalEvidenceOutcome.NotFound => "not_found",
                DurableJournalEvidenceOutcome.Cancelled => "cancelled",
                _ => throw new UnreachableException(),
            };
        }
    }
}
