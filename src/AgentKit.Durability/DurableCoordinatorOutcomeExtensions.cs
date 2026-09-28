// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

/// <summary>Projects <see cref="DurableCoordinatorOutcome"/> values onto stable bounded telemetry text.</summary>
internal static class DurableCoordinatorOutcomeExtensions
{
    extension(DurableCoordinatorOutcome outcome)
    {
        /// <summary>Gets the stable lowercase dimension value for this outcome.</summary>
        /// <returns>A bounded telemetry token that never varies with identities or content.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The outcome is not a defined enumeration member.</exception>
        internal string ToStableValue() => outcome switch
        {
            DurableCoordinatorOutcome.Completed => "completed",
            DurableCoordinatorOutcome.Started => "started",
            DurableCoordinatorOutcome.Reconciled => "reconciled",
            DurableCoordinatorOutcome.Committed => "committed",
            DurableCoordinatorOutcome.Cancelled => "cancelled",
            DurableCoordinatorOutcome.Failed => "failed",
            DurableCoordinatorOutcome.Unavailable => "unavailable",
            DurableCoordinatorOutcome.LeaseLost => "lease_lost",
            DurableCoordinatorOutcome.OperatorRequired => "operator_required",
            DurableCoordinatorOutcome.Incompatible => "incompatible",
            DurableCoordinatorOutcome.NotPossible => "not_possible",
            DurableCoordinatorOutcome.Denied => "denied",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown durable coordinator outcome."),
        };
    }
}
