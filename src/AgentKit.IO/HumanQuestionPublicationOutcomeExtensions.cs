// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Converts human-question publication outcomes to stable non-content diagnostic values.</summary>
internal static class HumanQuestionPublicationOutcomeExtensions
{
    extension(HumanQuestionPublicationOutcome outcome)
    {
        /// <summary>Returns the stable bounded value used in telemetry for one defined outcome.</summary>
        /// <returns>A lowercase protocol-stable terminal outcome value.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined.</exception>
        public string ToStableValue() => outcome switch
        {
            HumanQuestionPublicationOutcome.Answered => "answered",
            HumanQuestionPublicationOutcome.TimedOut => "timed_out",
            HumanQuestionPublicationOutcome.ChannelUnavailable => "channel_unavailable",
            HumanQuestionPublicationOutcome.GrantDenied => "grant_denied",
            HumanQuestionPublicationOutcome.CapturedAuthorizationMismatch => "authorization_mismatch",
            HumanQuestionPublicationOutcome.Cancelled => "cancelled",
            HumanQuestionPublicationOutcome.Failed => "failed",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "The human question publication outcome is undefined."),
        };
    }
}
