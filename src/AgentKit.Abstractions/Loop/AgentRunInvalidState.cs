// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The run cannot continue because its captured lifecycle evidence is internally inconsistent.</summary>
public sealed record AgentRunInvalidState: AgentRunOutcome
{
    /// <summary>Initializes an invalid-state outcome.</summary>
    /// <param name="safeMessage">Bounded diagnostic text containing no prompt, model, or tool content.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is null, empty, or whitespace.</exception>
    public AgentRunInvalidState(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets bounded content-free diagnostic text.</summary>
    public string SafeMessage { get; }
}
