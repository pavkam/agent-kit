// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory;

/// <summary>Maps finite execution-lease acquisition outcomes to stable diagnostic values.</summary>
internal static class DurableLeaseAcquisitionOutcomeExtensions
{
    extension(DurableLeaseAcquisitionOutcome outcome)
    {
        /// <summary>Returns the bounded lowercase value shared by acquisition logs, activities, and metrics.</summary>
        /// <returns>A stable lowercase terminal-outcome value.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined.</exception>
        internal string ToStableValue()
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(outcome);
            return outcome switch
            {
                DurableLeaseAcquisitionOutcome.GrantedFirstOwnership => "granted_first_ownership",
                DurableLeaseAcquisitionOutcome.GrantedByTakeover => "granted_by_takeover",
                DurableLeaseAcquisitionOutcome.HeldByAnotherWorker => "held_by_another_worker",
                DurableLeaseAcquisitionOutcome.Cancelled => "cancelled",
                DurableLeaseAcquisitionOutcome.Failed => "failed",
                _ => throw new UnreachableException(),
            };
        }
    }
}
