// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that an accepted, committed tool call is about to be invoked.</summary>
public sealed record AgentRunToolCallStarted: AgentRunEvent
{
    /// <summary>Initializes a tool-call start observation.</summary>
    /// <param name="turnId">The turn that requested the call.</param>
    /// <param name="call">The committed tool call.</param>
    /// <exception cref="ArgumentNullException"><paramref name="call"/> is null.</exception>
    public AgentRunToolCallStarted(TurnId turnId, ToolCallPart call)
    {
        ArgumentNullException.ThrowIfNull(call);
        TurnId = turnId;
        Call = call;
    }

    /// <summary>Gets the turn that requested the call.</summary>
    public TurnId TurnId { get; init; }

    /// <summary>Gets the committed call, including its typed correlation identity.</summary>
    public ToolCallPart Call { get; init; }
}
