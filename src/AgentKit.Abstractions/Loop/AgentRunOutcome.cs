// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The closed semantic outcome of one agent run. Canonical results use
/// <see cref="RunSucceeded"/>, <see cref="RunIdle"/>, <see cref="RunDeferred"/>,
/// <see cref="RunCancelled"/>, <see cref="RunLimitReached"/>, <see cref="RunPolicyHalted"/>,
/// or <see cref="RunFailed"/>. The reduced loop still exposes legacy cases including
/// <see cref="AgentRunCompleted"/>, <see cref="AgentRunFailed"/>,
/// <see cref="AgentRunCancelled"/>, <see cref="AgentRunTurnLimitReached"/>,
/// <see cref="AgentRunContextPreparationFailed"/>, <see cref="AgentRunIdle"/>,
/// <see cref="AgentRunInvalidState"/>, or <see cref="AgentRunOutputRejected"/>.
/// </summary>
/// <remarks>
/// This hierarchy is closed to first-party outcomes recognized by
/// <see cref="IAgentLoop"/> implementations and their callers; external
/// assemblies cannot construct additional cases by copying built-in values. Each instance is immutable
/// and safe to share across threads without synchronization. A run reaches
/// exactly one terminal outcome; limit exhaustion, cancellation, and
/// context-preparation failure are typed outcomes, never a thrown
/// <see cref="NotSupportedException"/> or a successful result with a null
/// output.
/// The additive continuation outcomes coexist with the original loop outcomes
/// while consumers migrate to handling the complete family explicitly.
/// </remarks>
public abstract record AgentRunOutcome
{
    /// <summary>Prevents outcome implementations outside this assembly while allowing the canonical terminal records to initialize their base state.</summary>
    /// <remarks>The base owns no settlement state; callers must keep semantic outcome separate from durable publication and recovery status.</remarks>
    private protected AgentRunOutcome()
    {
    }

    /// <summary>Copies only the same built-in semantic variant, preventing external variants from copying an existing outcome.</summary>
    /// <param name="original">The nonnull outcome with the same concrete runtime type.</param>
    /// <exception cref="ArgumentNullException">The original is null.</exception>
    /// <exception cref="ArgumentException">The original has a different concrete runtime type.</exception>
    /// <remarks>Record inheritance requires a protected copy constructor. Canonical and legacy same-variant copies remain valid without reopening the outcome family.</remarks>
    protected AgentRunOutcome(AgentRunOutcome original)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentException.ThrowIfNotEqual(original.GetType(), GetType(), nameof(original));
    }
}
