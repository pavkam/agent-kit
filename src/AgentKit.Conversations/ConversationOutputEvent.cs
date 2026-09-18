// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Carries the validated structured output a turn's selected <see cref="OutputDefinition"/> accepted.</summary>
/// <remarks>
/// Emitted at most once per turn, after every text, tool, and usage event of that turn and before the completion
/// event, and only when the conversation selects an output definition and the run completed with an accepted
/// value. The same value is available as <see cref="ConversationTurnResult.Output"/>. Rejected candidates are
/// not events: the turn ends with a failed completion whose safe description names the rejection.
/// </remarks>
public sealed record ConversationOutputEvent: ConversationEvent
{
    /// <summary>Initializes an output event.</summary>
    /// <param name="output">The accepted, validated output.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is null.</exception>
    public ConversationOutputEvent(ValidatedOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        Output = output;
    }

    /// <summary>Gets the accepted, validated output.</summary>
    public ValidatedOutput Output { get; }
}
