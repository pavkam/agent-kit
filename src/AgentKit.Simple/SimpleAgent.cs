// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple;

/// <summary>
/// One ready-to-use agent over one conversation. Ask it questions with <see cref="AskAsync"/>, or reach the
/// full <see cref="IConversationSession"/> through <see cref="Conversation"/> for live events, tool
/// presentations, history, and session resume.
/// </summary>
/// <remarks>
/// The agent owns the service provider it was built with and disposes it exactly once. Turns are
/// serialized by the underlying conversation; complete or cancel an in-flight call before disposing.
/// Successive calls continue the same conversation.
/// </remarks>
public sealed class SimpleAgent: IDisposable
{
    private readonly OwnedConversationSession _conversation;

    /// <summary>Initializes an agent over a conversation and the composition that owns it.</summary>
    /// <param name="conversation">The composed conversation.</param>
    /// <param name="owner">The service provider to dispose with this agent.</param>
    /// <exception cref="ArgumentNullException"><paramref name="conversation"/> or <paramref name="owner"/> is null.</exception>
    internal SimpleAgent(IConversationSession conversation, IDisposable owner)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ArgumentNullException.ThrowIfNull(owner);
        _conversation = new OwnedConversationSession(conversation, owner);
    }

    /// <summary>Gets the underlying conversation for everything beyond plain text: live events, history, resume, and tool presentations.</summary>
    public IConversationSession Conversation => _conversation;

    /// <summary>Sends one user message and returns the assistant's reply as text.</summary>
    /// <param name="text">The user message.</param>
    /// <param name="cancellationToken">Cancels the caller's wait; committed work is preserved.</param>
    /// <returns>The concatenated assistant text of the completed turn.</returns>
    /// <exception cref="ArgumentException"><paramref name="text"/> is blank.</exception>
    /// <exception cref="SimpleAgentException">The turn did not end with a final assistant message; the message carries the safe reason.</exception>
    /// <exception cref="OperationCanceledException">The wait was cancelled.</exception>
    /// <exception cref="ObjectDisposedException">The agent was disposed.</exception>
    public async Task<string> AskAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var result = await _conversation.SendAsync(text, cancellationToken).ConfigureAwait(false);
        var reply = string.Concat(result.Events.OfType<ConversationAssistantTextEvent>().Select(static e => e.Text));
        return result.Succeeded
            ? reply
            : throw new SimpleAgentException(reply.Length > 0 ? reply : "The run ended without a final assistant message.", result);
    }

    /// <summary>Sends one user message and returns the complete turn result, including tool and usage events.</summary>
    /// <param name="text">The user message.</param>
    /// <param name="cancellationToken">Cancels the caller's wait; committed work is preserved.</param>
    /// <returns>The committed turn projection.</returns>
    /// <exception cref="ArgumentException"><paramref name="text"/> is blank.</exception>
    /// <exception cref="ObjectDisposedException">The agent was disposed.</exception>
    public Task<ConversationTurnResult> SendAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return _conversation.SendAsync(text, cancellationToken);
    }

    /// <summary>Sends one user message while streaming events to <paramref name="observer"/>, then returns the committed turn result.</summary>
    /// <param name="text">The user message.</param>
    /// <param name="observer">Receives text and reasoning deltas, tool starts and results, usage, and one completion event.</param>
    /// <param name="cancellationToken">Cancels the caller's wait; committed work is preserved.</param>
    /// <returns>The committed turn projection.</returns>
    /// <exception cref="ArgumentException"><paramref name="text"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="observer"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The agent was disposed.</exception>
    public Task<ConversationTurnResult> SendAsync(string text, IConversationEventObserver observer, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(observer);
        return _conversation.SendAsync(text, observer, cancellationToken);
    }

    /// <summary>Releases the composition. Repeated calls are harmless.</summary>
    public void Dispose() => _conversation.Dispose();
}
