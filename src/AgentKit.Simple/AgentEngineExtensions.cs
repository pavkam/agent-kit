// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple;

/// <summary>
/// Talks to the one conversation an engine built with the <see cref="AgentEngineBuilderExtensions"/> sugar
/// hosts: plain text in and out with <see cref="AskAsync"/>, or the full turn through <c>SendAsync</c>.
/// </summary>
/// <remarks>
/// Successive calls continue the same conversation. The conversation is a singleton owned by the engine's
/// provider and is released when the engine is disposed; complete or cancel an in-flight call first.
/// </remarks>
public static class AgentEngineExtensions
{
    extension(AgentEngine engine)
    {
        /// <summary>Gets the engine's conversation for everything beyond plain text: live events, history, resume, and tool presentations.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="engine"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The engine was not built with the <see cref="AgentEngineBuilderExtensions"/> sugar and hosts no conversation.</exception>
        public IConversationSession Conversation
        {
            get
            {
                ArgumentNullException.ThrowIfNull(engine);
                return engine.Services.GetService<IConversationSession>()
                    ?? throw new InvalidOperationException(
                        "This engine hosts no conversation. Build it with UseLocalDevelopmentDefaults/UseOpenAI (or register AddConversationSession) before calling AskAsync.");
            }
        }

        /// <summary>Sends one user message and returns the assistant's reply as text.</summary>
        /// <param name="text">The user message.</param>
        /// <param name="cancellationToken">Cancels the caller's wait; committed work is preserved.</param>
        /// <returns>The concatenated assistant text of the completed turn.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="engine"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="text"/> is blank.</exception>
        /// <exception cref="SimpleAgentException">The turn did not end with a final assistant message; the message carries the safe reason.</exception>
        /// <exception cref="OperationCanceledException">The wait was cancelled.</exception>
        public async Task<string> AskAsync(string text, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(engine);
            ArgumentException.ThrowIfNullOrWhiteSpace(text);
            var result = await engine.Conversation.SendAsync(text, cancellationToken).ConfigureAwait(false);
            var reply = string.Concat(result.Events.OfType<ConversationAssistantTextEvent>().Select(static e => e.Text));
            return result.Succeeded
                ? reply
                : throw new SimpleAgentException(reply.Length > 0 ? reply : "The run ended without a final assistant message.", result);
        }

        /// <summary>Sends one user message and returns the complete turn result, including tool and usage events.</summary>
        /// <param name="text">The user message.</param>
        /// <param name="cancellationToken">Cancels the caller's wait; committed work is preserved.</param>
        /// <returns>The committed turn projection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="engine"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="text"/> is blank.</exception>
        public Task<ConversationTurnResult> SendAsync(string text, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(engine);
            ArgumentException.ThrowIfNullOrWhiteSpace(text);
            return engine.Conversation.SendAsync(text, cancellationToken);
        }

        /// <summary>Sends one user message while streaming events to <paramref name="observer"/>, then returns the committed turn result.</summary>
        /// <param name="text">The user message.</param>
        /// <param name="observer">Receives text and reasoning deltas, tool starts and results, usage, and one completion event.</param>
        /// <param name="cancellationToken">Cancels the caller's wait; committed work is preserved.</param>
        /// <returns>The committed turn projection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="engine"/> or <paramref name="observer"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="text"/> is blank.</exception>
        public Task<ConversationTurnResult> SendAsync(string text, IConversationEventObserver observer, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(engine);
            ArgumentException.ThrowIfNullOrWhiteSpace(text);
            ArgumentNullException.ThrowIfNull(observer);
            return engine.Conversation.SendAsync(text, observer, cancellationToken);
        }
    }
}
