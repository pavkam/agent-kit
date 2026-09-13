// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory;

/// <summary>Maps finite execution-lease renewal outcomes to stable diagnostic values.</summary>
internal static class DurableLeaseRenewalOutcomeExtensions
{
    extension(DurableLeaseRenewalOutcome outcome)
    {
        /// <summary>Returns the bounded lowercase value shared by renewal logs, activities, and metrics.</summary>
        /// <returns>A stable lowercase terminal-outcome value.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined.</exception>
        internal string ToStableValue()
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(outcome);
            return outcome switch
            {
                DurableLeaseRenewalOutcome.Renewed => "renewed",
                DurableLeaseRenewalOutcome.Lost => "lost",
                DurableLeaseRenewalOutcome.Cancelled => "cancelled",
                DurableLeaseRenewalOutcome.Failed => "failed",
                _ => throw new UnreachableException(),
            };
        }
    }
}
