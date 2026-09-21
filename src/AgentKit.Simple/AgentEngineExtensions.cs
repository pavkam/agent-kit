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

        /// <summary>Subscribes to one turn through the engine's typed streaming surface.</summary>
        /// <typeparam name="TOutput">The validated output type configured on the engine.</typeparam>
        /// <param name="text">The user message.</param>
        /// <param name="cancellationToken">Cancels admission and the drive. Disposing the returned stream does not.</param>
        /// <returns>The started stream, or a typed rejection when admission fails before acceptance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="engine"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="text"/> is blank.</exception>
        /// <exception cref="InvalidOperationException">The conversation is not bound to a session yet.</exception>
        public Task<AgentRunStreamStartResult<TOutput>> StreamAsync<TOutput>(string text, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(engine);
            ArgumentException.ThrowIfNullOrWhiteSpace(text);
            var plan = engine.Services.GetRequiredService<SimpleAgentPlan>();
            var sessionId = engine.Conversation.SessionId
                ?? throw new InvalidOperationException("Stream a turn only after the conversation is bound to a session.");
            var inputId = engine.Services.GetRequiredService<IIdentifierGenerator<InputId>>().Create();
            return engine.StreamAsync<TOutput>(
                new AgentRunRequest(
                    plan.EffectiveAgentId,
                    sessionId,
                    conversationId: null,
                    engine.Identity,
                    new AgentInput(
                        inputId,
                        InputDelivery.Steer,
                        [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)],
                        ExtensionData.Empty)),
                cancellationToken);
        }

        /// <summary>Requests durable abort for one active run of this engine's default agent.</summary>
        /// <param name="runId">The accepted run to abort.</param>
        /// <param name="cancellationToken">Cancels the wait before the abort commits.</param>
        /// <returns>The session store's typed abort outcome.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="engine"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="runId"/> is default.</exception>
        /// <exception cref="InvalidOperationException">The engine was not built through the <c>AgentKit.Simple</c> sugar.</exception>
        /// <exception cref="AgentAdmissionRejectedException">The run is not active in this process.</exception>
        public async Task<SessionRunAbortResult> CancelAsync(RunId runId, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(engine);
            ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
            var plan = engine.Services.GetRequiredService<SimpleAgentPlan>();
            var resolution = await engine.GetAgentAsync(plan.EffectiveAgentId, cancellationToken).ConfigureAwait(false);
            var agent = resolution is ResolvedAgent resolved
                ? resolved.Agent
                : throw new InvalidOperationException("The simple agent is not available in the engine catalog.");
            return await agent.CancelAsync(runId, engine.Identity, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Attaches to one active run's durable replay and live event tail for this engine's default agent.</summary>
        /// <typeparam name="TOutput">The validated output snapshot type.</typeparam>
        /// <param name="runId">The accepted run to attach to.</param>
        /// <param name="cancellationToken">Cancels attachment setup. It does not abort the run.</param>
        /// <returns>A started stream, or a rejection when the run settled or cannot be tailed.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="engine"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="runId"/> is default.</exception>
        /// <exception cref="InvalidOperationException">The engine was not built through the <c>AgentKit.Simple</c> sugar.</exception>
        public async Task<AgentRunStreamStartResult<TOutput>> AttachAsync<TOutput>(
            RunId runId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(engine);
            ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
            var sessionId = engine.Conversation.SessionId
                ?? throw new InvalidOperationException("Attach to a run only after the conversation is bound to a session.");
            var plan = engine.Services.GetRequiredService<SimpleAgentPlan>();
            var resolution = await engine.GetAgentAsync(plan.EffectiveAgentId, cancellationToken).ConfigureAwait(false);
            var agent = resolution is ResolvedAgent resolved
                ? resolved.Agent
                : throw new InvalidOperationException("The simple agent is not available in the engine catalog.");
            return await agent.AttachAsync<TOutput>(runId, sessionId, engine.Identity, cancellationToken).ConfigureAwait(false);
        }
    }
}
