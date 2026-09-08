// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Proposes a supported non-success terminal outcome without claiming settlement.</summary>
public sealed record HaltRun: RunContinuationDecision
{
    /// <summary>Initializes a halt proposal.</summary>
    /// <param name="outcome">The non-success terminal outcome to preserve.</param>
    /// <exception cref="ArgumentNullException"><paramref name="outcome"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="outcome"/> is a successful semantic outcome.</exception>
    public HaltRun(AgentRunOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentException.ThrowIfSuccessfulRunOutcome(outcome);
        Outcome = outcome;
    }

    /// <summary>Gets the proposed non-success semantic outcome.</summary>
    public AgentRunOutcome Outcome { get; }
}
