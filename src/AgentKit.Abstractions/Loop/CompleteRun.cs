// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Proposes successful or idle semantic completion without committing an outcome or claiming settlement.</summary>
/// <remarks>The session owner must reject this proposal if revalidation finds pending work, required output validation, a stop condition, or a changed operation state.</remarks>
public sealed record CompleteRun: RunContinuationDecision
{
    /// <summary>Initializes a proposal for a successful-output or idle semantic outcome.</summary>
    /// <param name="outcome">The non-null successful-output or idle outcome proposed for the run.</param>
    /// <exception cref="ArgumentNullException"><paramref name="outcome"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="outcome"/> is not a successful semantic outcome.</exception>
    public CompleteRun(AgentRunOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentException.ThrowIfNotSuccessfulRunOutcome(outcome);
        Outcome = outcome;
    }

    /// <summary>Gets the proposed successful semantic outcome.</summary>
    /// <value>A non-null success or idle outcome; obtaining it does not publish output or settle the run.</value>
    public AgentRunOutcome Outcome { get; }
}
