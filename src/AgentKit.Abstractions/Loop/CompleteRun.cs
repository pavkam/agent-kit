// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Proposes successful or idle semantic completion without claiming settlement.</summary>
public sealed record CompleteRun: RunContinuationDecision
{
    /// <summary>Initializes a successful completion proposal.</summary>
    /// <param name="outcome">A successful-output or idle outcome.</param>
    /// <exception cref="ArgumentNullException"><paramref name="outcome"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="outcome"/> is not a successful semantic outcome.</exception>
    public CompleteRun(AgentRunOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentException.ThrowIfNotSuccessfulRunOutcome(outcome);
        Outcome = outcome;
    }

    /// <summary>Gets the proposed successful semantic outcome.</summary>
    public AgentRunOutcome Outcome { get; }
}
