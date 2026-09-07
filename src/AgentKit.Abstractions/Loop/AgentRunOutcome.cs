// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The closed terminal outcome of one agent run: exactly one of
/// <see cref="AgentRunCompleted"/>, <see cref="AgentRunFailed"/>,
/// <see cref="AgentRunCancelled"/>, <see cref="AgentRunTurnLimitReached"/>,
/// or <see cref="AgentRunContextPreparationFailed"/>.
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
/// </remarks>
public abstract record AgentRunOutcome
{
    private protected AgentRunOutcome()
    {
    }
}
