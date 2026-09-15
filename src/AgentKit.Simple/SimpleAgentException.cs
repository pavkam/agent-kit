// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple;

/// <summary>
/// Thrown by <see cref="SimpleAgent.AskAsync"/> when a turn ends without a final assistant message: a turn
/// limit, a provider failure, a rejected output, a cancelled run, or a session problem.
/// </summary>
/// <remarks>
/// The message is the same bounded, safe description the conversation would have shown a user; it never
/// carries provider diagnostics or credentials. <see cref="Result"/> exposes the committed events so a
/// caller that wants more than text can inspect what happened.
/// </remarks>
public sealed class SimpleAgentException: Exception
{
    /// <summary>Initializes the exception with the safe description and the committed turn result.</summary>
    /// <param name="message">The safe, user-presentable reason.</param>
    /// <param name="result">The committed turn projection.</param>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
    public SimpleAgentException(string message, ConversationTurnResult result)
        : base(message)
    {
        ArgumentNullException.ThrowIfNull(result);
        Result = result;
    }

    /// <summary>Gets the committed turn projection the failure was derived from.</summary>
    public ConversationTurnResult Result { get; }
}
