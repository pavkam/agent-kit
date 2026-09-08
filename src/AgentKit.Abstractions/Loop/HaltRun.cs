// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Proposes a supported non-success semantic terminal outcome without committing it or claiming settlement.</summary>
/// <remarks>The session owner preserves the typed outcome only after it revalidates the proposal; this value cannot turn a successful outcome into failure or bypass the run's terminal-state rules.</remarks>
public sealed record HaltRun: RunContinuationDecision
{
    /// <summary>Initializes a proposal to halt with a non-success terminal outcome.</summary>
    /// <param name="outcome">The non-null non-success outcome proposed for the open run.</param>
    /// <exception cref="ArgumentNullException"><paramref name="outcome"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="outcome"/> is a successful semantic outcome.</exception>
    public HaltRun(AgentRunOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentException.ThrowIfSuccessfulRunOutcome(outcome);
        Outcome = outcome;
    }

    /// <summary>Gets the proposed non-success semantic outcome.</summary>
    /// <value>A non-null non-success outcome that remains uncommitted until the session owner accepts the proposal.</value>
    public AgentRunOutcome Outcome { get; }
}
