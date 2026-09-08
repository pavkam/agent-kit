// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Maps security-audit dispatch outcomes to stable bounded diagnostic values.</summary>
internal static class SecurityAuditDispatchOutcomeExtensions
{
    extension(SecurityAuditDispatchOutcome outcome)
    {
        /// <summary>Gets the stable diagnostic value for one defined dispatch outcome.</summary>
        /// <returns>A lower-case bounded value suitable for metrics and activity status.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined.</exception>
        public string ToStableValue() => outcome switch
        {
            SecurityAuditDispatchOutcome.Accepted => "accepted",
            SecurityAuditDispatchOutcome.Unavailable => "unavailable",
            SecurityAuditDispatchOutcome.Failed => "failed",
            SecurityAuditDispatchOutcome.TimedOut => "timed_out",
            SecurityAuditDispatchOutcome.Cancelled => "cancelled",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "The security audit dispatch outcome is undefined."),
        };
    }
}
