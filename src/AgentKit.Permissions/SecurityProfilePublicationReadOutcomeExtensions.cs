// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Maps publication-read outcomes to stable bounded diagnostic values.</summary>
internal static class SecurityProfilePublicationReadOutcomeExtensions
{
    extension(SecurityProfilePublicationReadOutcome outcome)
    {
        /// <summary>Gets the stable diagnostic value for one defined outcome.</summary>
        /// <returns>A lower-case bounded value suitable for metrics and structured logs.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined.</exception>
        public string ToStableValue() => outcome switch
        {
            SecurityProfilePublicationReadOutcome.Found => "found",
            SecurityProfilePublicationReadOutcome.Unavailable => "unavailable",
            SecurityProfilePublicationReadOutcome.Cancelled => "cancelled",
            _ => throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "The security-profile publication-read outcome is undefined."),
        };
    }
}
