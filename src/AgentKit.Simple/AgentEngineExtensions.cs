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

        /// <summary>
        /// Gets the identity the builder's sugar composed every turn with: the one passed to <c>WithIdentity</c>, or
        /// the local-development identity when <c>UseLocalDevelopmentDefaults</c> was used.
        /// </summary>
        /// <value>The identity to pass to <see cref="AgentSendRequest"/> when driving a hosted agent directly.</value>
        /// <exception cref="ArgumentNullException"><paramref name="engine"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The engine was not built through the <c>AgentKit.Simple</c> sugar.</exception>
        public ExecutionIdentity Identity
        {
            get
            {
                ArgumentNullException.ThrowIfNull(engine);
                return (engine.Services.GetService<SimpleAgentPlan>()
                    ?? throw new InvalidOperationException("This engine was not composed with the AgentKit.Simple builder methods; it carries no default identity."))
                    .RequireIdentity();
            }
        }

        /// <summary>
        /// Sends one user message and returns the validated structured answer as <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The runtime type the engine's output definition deserializes into; see <c>WithOutput&lt;T&gt;</c>.</typeparam>
        /// <param name="text">The user's message.</param>
        /// <param name="cancellationToken">Cancels the turn.</param>
        /// <returns>The accepted, deserialized value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="engine"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="text"/> is blank.</exception>
        /// <exception cref="SimpleAgentException">
        /// The turn did not complete, the engine selects no output definition, the accepted output carries no
        /// deserialized value, or the value is not a <typeparamref name="T"/>. The exception carries the safe reason
        /// and the committed <see cref="ConversationTurnResult"/>.
        /// </exception>
        public async Task<T> AskAsync<T>(string text, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(engine);
            ArgumentException.ThrowIfNullOrWhiteSpace(text);
            var result = await engine.Conversation.SendAsync(text, cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                var reply = string.Concat(result.Events.OfType<ConversationAssistantTextEvent>().Select(static e => e.Text));
                throw new SimpleAgentException(reply.Length > 0 ? reply : "The run ended without a final assistant message.", result);
            }

            return result.Output switch
            {
                null => throw new SimpleAgentException(
                    "The turn completed without structured output; configure the engine with WithOutput<T> before calling AskAsync<T>.", result),
                { Value: T typed } => typed,
                { Value: null } => throw new SimpleAgentException(
                    "The accepted output declares no runtime type; use WithOutput<T> so the value is deserialized.", result),
                { Value: var other } => throw new SimpleAgentException(
                    $"The accepted output is a '{other.GetType().Name}', not a '{typeof(T).Name}'.", result),
            };
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
