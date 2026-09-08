// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The closed terminal outcome of one agent run: exactly one of
/// <see cref="AgentRunCompleted"/>, <see cref="AgentRunFailed"/>,
/// <see cref="AgentRunCancelled"/>, <see cref="AgentRunTurnLimitReached"/>,
/// <see cref="AgentRunContextPreparationFailed"/>, <see cref="AgentRunIdle"/>,
/// <see cref="AgentRunInvalidState"/>, or <see cref="AgentRunOutputRejected"/>.
/// </summary>
/// <remarks>
/// This hierarchy is closed to first-party outcomes recognized by
/// <see cref="IAgentLoop"/> implementations and their callers; external
/// assemblies cannot derive additional cases. Each instance is immutable
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
}
