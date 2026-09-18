// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The run reached a final assistant message with no further tool calls to resolve.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record AgentRunCompleted: AgentRunOutcome
{
    /// <summary>Initializes a new instance of the <see cref="AgentRunCompleted"/> record.</summary>
    /// <param name="finalMessage">The final, complete assistant message.</param>
    /// <exception cref="ArgumentNullException"><paramref name="finalMessage"/> is null.</exception>
    public AgentRunCompleted(AssistantMessage finalMessage)
    {
        ArgumentNullException.ThrowIfNull(finalMessage);
        FinalMessage = finalMessage;
    }

    /// <summary>Gets the final, complete assistant message.</summary>
    public AssistantMessage FinalMessage { get; init; }

    /// <summary>Gets the validated structured output the run's selected definition accepted, when one was selected.</summary>
    /// <value>
    /// The accepted output extracted from <see cref="FinalMessage"/> and validated by the run's
    /// <see cref="IOutputProcessor"/>, or <see langword="null"/> for a free-text run. A run with an output
    /// definition never completes without it; a rejected candidate settles as <see cref="AgentRunOutputRejected"/>.
    /// </value>
    public ValidatedOutput? Output { get; init; }
}
