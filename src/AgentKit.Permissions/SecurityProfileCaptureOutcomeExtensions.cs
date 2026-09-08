// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Maps profile-capture outcomes to stable bounded diagnostic values.</summary>
internal static class SecurityProfileCaptureOutcomeExtensions
{
    extension(SecurityProfileCaptureOutcome outcome)
    {
        /// <summary>Gets the stable diagnostic value for one defined outcome.</summary>
        /// <returns>A lower-case bounded value suitable for metrics and activity status.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined.</exception>
        public string ToStableValue() => outcome switch
        {
            SecurityProfileCaptureOutcome.Captured => "captured",
            SecurityProfileCaptureOutcome.Unavailable => "unavailable",
            SecurityProfileCaptureOutcome.MismatchedPublication => "mismatched_publication",
            SecurityProfileCaptureOutcome.Cancelled => "cancelled",
            SecurityProfileCaptureOutcome.Failed => "failed",
            _ => throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "The security-profile capture outcome is undefined."),
        };
    }
}
