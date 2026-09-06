// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The outcome of evaluating one session against the configured retention
/// policy.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. The
/// retention policy only decides eligibility; the session store performs
/// the actual protected mutation (archive or delete) when a caller acts on
/// this decision.
/// </remarks>
public sealed record SessionRetentionDecision
{
    /// <summary>Initializes a new instance of the <see cref="SessionRetentionDecision"/> record.</summary>
    /// <param name="action">The decided action.</param>
    /// <param name="reason">A human-readable explanation for the decision, when useful for audit.</param>
    public SessionRetentionDecision(SessionRetentionAction action, string? reason)
    {
        Action = action;
        Reason = reason;
    }

    /// <summary>Gets the decided action.</summary>
    public SessionRetentionAction Action { get; init; }

    /// <summary>Gets a human-readable explanation for the decision, when useful for audit.</summary>
    public string? Reason { get; init; }
}
