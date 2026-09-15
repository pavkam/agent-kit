// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports the terminal result produced for one accepted tool call.</summary>
public sealed record AgentRunToolCallCompleted: AgentRunEvent
{
    /// <summary>Initializes a terminal tool-call observation.</summary>
    /// <param name="turnId">The turn that requested the call.</param>
    /// <param name="result">The terminal history projection correlated by <see cref="ToolResultPart.CallId"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
    public AgentRunToolCallCompleted(TurnId turnId, ToolResultPart result)
    {
        ArgumentNullException.ThrowIfNull(result);
        TurnId = turnId;
        Result = result;
    }

    /// <summary>Gets the turn that requested the call.</summary>
    public TurnId TurnId { get; init; }

    /// <summary>Gets the terminal result projection and typed correlation identity.</summary>
    public ToolResultPart Result { get; init; }
}
