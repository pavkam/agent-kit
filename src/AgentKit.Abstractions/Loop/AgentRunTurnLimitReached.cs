// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The run was halted because it reached <see cref="AgentRunRequest.MaxTurns"/>
/// while tool calls were still pending resolution.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. Every
/// message committed before the limit was reached remains in durable
/// history; this outcome never discards committed work.
/// </remarks>
public sealed record AgentRunTurnLimitReached: AgentRunOutcome
{
    /// <summary>Initializes a new instance of the <see cref="AgentRunTurnLimitReached"/> record.</summary>
    /// <param name="maxTurns">The configured maximum number of turns that was reached.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxTurns"/> is not positive.</exception>
    public AgentRunTurnLimitReached(int maxTurns)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxTurns, 0);
        MaxTurns = maxTurns;
    }

    /// <summary>Gets the configured maximum number of turns that was reached.</summary>
    public int MaxTurns { get; init; }
}
