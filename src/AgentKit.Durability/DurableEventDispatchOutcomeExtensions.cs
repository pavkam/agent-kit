// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

/// <summary>Projects <see cref="DurableEventDispatchOutcome"/> values onto stable bounded telemetry text.</summary>
internal static class DurableEventDispatchOutcomeExtensions
{
    extension(DurableEventDispatchOutcome outcome)
    {
        /// <summary>Gets the stable lowercase dimension value for this outcome.</summary>
        /// <returns>A bounded telemetry token that never varies with identities or content.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The outcome is not a defined enumeration member.</exception>
        internal string ToStableValue() => outcome switch
        {
            DurableEventDispatchOutcome.Published => "published",
            DurableEventDispatchOutcome.SinkSkipped => "sink_skipped",
            DurableEventDispatchOutcome.SinkFailed => "sink_failed",
            DurableEventDispatchOutcome.RequiredSinkUnavailable => "required_sink_unavailable",
            DurableEventDispatchOutcome.RequiredSinkFailed => "required_sink_failed",
            DurableEventDispatchOutcome.Cancelled => "cancelled",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown durable event dispatch outcome."),
        };
    }
}
