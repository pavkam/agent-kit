// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Converts defined task-delegation publication outcomes to stable telemetry values.</summary>
internal static class TaskDelegationPublicationOutcomeExtensions
{
    extension(TaskDelegationPublicationOutcome outcome)
    {
        /// <summary>Gets the bounded stable value exported for a defined terminal outcome.</summary>
        /// <returns>The non-empty bounded outcome value.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined.</exception>
        internal string ToStableValue() => outcome switch
        {
            TaskDelegationPublicationOutcome.Dispatched => "dispatched",
            TaskDelegationPublicationOutcome.ChannelRejected => "channel_rejected",
            TaskDelegationPublicationOutcome.GrantDenied => "grant_denied",
            TaskDelegationPublicationOutcome.CapturedAuthorizationMismatch => "authorization_mismatch",
            TaskDelegationPublicationOutcome.Cancelled => "cancelled",
            TaskDelegationPublicationOutcome.Failed => "failed",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "The task delegation publication outcome is undefined."),
        };
    }
}
