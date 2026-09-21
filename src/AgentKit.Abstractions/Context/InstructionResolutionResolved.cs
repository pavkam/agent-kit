// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Instruction resolution produced ordered, provider-ready instruction messages.</summary>
public sealed record InstructionResolutionResolved: InstructionResolutionResult
{
    /// <summary>Initializes a successful resolution.</summary>
    /// <param name="messages">The ordered instruction messages to place before conversational history.</param>
    /// <exception cref="ArgumentException"><paramref name="messages"/> is a default array or contains null.</exception>
    public InstructionResolutionResolved(ImmutableArray<AgentMessage> messages)
    {
        ArgumentException.ThrowIfContainsNull(messages);
        Messages = messages;
    }

    /// <summary>Gets the ordered instruction messages to place before conversational history.</summary>
    public ImmutableArray<AgentMessage> Messages { get; }
}
